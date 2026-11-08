using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Chunking;
using AsistenFile.Core.Config;
using AsistenFile.Core.Embeddings;
using AsistenFile.Core.Extractors;
using AsistenFile.Core.Models;
using AsistenFile.Core.Storage;

namespace AsistenFile.Core.Indexing
{
    public class IndexingProgressEventArgs : EventArgs
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int IndexedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int ErrorFiles { get; set; }
        public string CurrentFileName { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public int PercentComplete => TotalFiles > 0 ? (int)((ProcessedFiles / (float)TotalFiles) * 100) : 0;
    }

    public interface IIndexingService
    {
        bool IsIndexing { get; }
        event EventHandler<IndexingProgressEventArgs>? ProgressChanged;
        event EventHandler<string>? StatusChanged;
        Task<int> ScanAndIndexAllAsync(CancellationToken ct = default);
        Task<int> ScanAndIndexFolderAsync(string folderPath, CancellationToken ct = default);
        Task IndexSingleFileAsync(string filePath, CancellationToken ct = default);
        Task RemoveFileAsync(string filePath);
    }

    public class IndexingService : IIndexingService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IExtractorFactory _extractorFactory;
        private readonly ITextChunker _chunker;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISettingsService _settingsService;
        private readonly SemaphoreSlim _indexingLock = new(1, 1);

        public bool IsIndexing { get; private set; }
        public event EventHandler<IndexingProgressEventArgs>? ProgressChanged;
        public event EventHandler<string>? StatusChanged;

        public IndexingService(
            IDatabaseService databaseService,
            IExtractorFactory extractorFactory,
            ITextChunker chunker,
            IEmbeddingService embeddingService,
            ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _extractorFactory = extractorFactory;
            _chunker = chunker;
            _embeddingService = embeddingService;
            _settingsService = settingsService;
        }

        public async Task<int> ScanAndIndexAllAsync(CancellationToken ct = default)
        {
            if (IsIndexing) return 0;
            await _indexingLock.WaitAsync(ct);
            IsIndexing = true;

            try
            {
                var folders = await _databaseService.GetFoldersAsync();
                int totalIndexed = 0;

                foreach (var folder in folders)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!folder.IsActive || !Directory.Exists(folder.FolderPath)) continue;

                    totalIndexed += await ProcessFolderInternalAsync(folder, ct);
                }

                StatusChanged?.Invoke(this, $"Pengindeksan selesai. Total file terindeks: {totalIndexed}.");
                return totalIndexed;
            }
            finally
            {
                IsIndexing = false;
                _indexingLock.Release();
            }
        }

        public async Task<int> ScanAndIndexFolderAsync(string folderPath, CancellationToken ct = default)
        {
            if (IsIndexing) return 0;
            await _indexingLock.WaitAsync(ct);
            IsIndexing = true;

            try
            {
                var folder = await _databaseService.AddFolderAsync(folderPath);
                var count = await ProcessFolderInternalAsync(folder, ct);
                StatusChanged?.Invoke(this, $"Folder '{Path.GetFileName(folderPath)}' selesai diindeks ({count} file).");
                return count;
            }
            finally
            {
                IsIndexing = false;
                _indexingLock.Release();
            }
        }

        private async Task<int> ProcessFolderInternalAsync(IndexedFolder folder, CancellationToken ct)
        {
            StatusChanged?.Invoke(this, $"Memindai file dalam '{folder.FolderPath}'...");

            var candidateFiles = DiscoverFiles(folder.FolderPath);
            int total = candidateFiles.Count;
            int processed = 0;
            int indexedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            var progress = new IndexingProgressEventArgs
            {
                TotalFiles = total,
                ProcessedFiles = 0
            };
            ProgressChanged?.Invoke(this, progress);

            foreach (var filePath in candidateFiles)
            {
                if (ct.IsCancellationRequested) break;

                processed++;
                var fileName = Path.GetFileName(filePath);
                progress.ProcessedFiles = processed;
                progress.CurrentFileName = fileName;

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists) continue;

                    var existing = await _databaseService.GetFileByPathAsync(filePath);
                    // Incremental scan check: skip if file modified time & size match DB record
                    if (existing != null &&
                        existing.Status == "Indexed" &&
                        Math.Abs((existing.LastModified - fileInfo.LastWriteTime).TotalSeconds) < 2 &&
                        existing.FileSizeBytes == fileInfo.Length)
                    {
                        skippedCount++;
                        progress.SkippedFiles = skippedCount;
                        ProgressChanged?.Invoke(this, progress);
                        continue;
                    }

                    // Index file
                    await IndexFileRecordAsync(filePath, folder.Id, ct);
                    indexedCount++;
                    progress.IndexedFiles = indexedCount;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    progress.ErrorFiles = errorCount;
                    // Log error to DB for file
                    await _databaseService.UpsertFileAsync(new DocumentFile
                    {
                        FilePath = filePath,
                        FileName = fileName,
                        Extension = Path.GetExtension(filePath).ToLowerInvariant(),
                        FileSizeBytes = 0,
                        LastModified = DateTime.Now,
                        IndexedAt = DateTime.Now,
                        Status = "Error",
                        ErrorMessage = ex.Message,
                        FolderId = folder.Id
                    });
                }

                ProgressChanged?.Invoke(this, progress);

                // Small cooperative yield to ensure zero UI freezing & low laptop resource utilization
                await Task.Delay(5, ct);
            }

            await _databaseService.UpdateFolderScanTimeAsync(folder.Id, DateTime.Now, candidateFiles.Count);

            progress.IsCompleted = true;
            ProgressChanged?.Invoke(this, progress);

            return indexedCount;
        }

        public async Task IndexSingleFileAsync(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath)) return;
            if (IsExcluded(filePath)) return;

            var folders = await _databaseService.GetFoldersAsync();
            long folderId = 0;
            foreach (var f in folders)
            {
                if (filePath.StartsWith(f.FolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    folderId = f.Id;
                    break;
                }
            }

            await IndexFileRecordAsync(filePath, folderId, ct);
            StatusChanged?.Invoke(this, $"File diperbarui: {Path.GetFileName(filePath)}");
        }

        public async Task RemoveFileAsync(string filePath)
        {
            await _databaseService.DeleteFileAndChunksAsync(filePath);
            StatusChanged?.Invoke(this, $"File dihapus dari indeks: {Path.GetFileName(filePath)}");
        }

        private async Task IndexFileRecordAsync(string filePath, long folderId, CancellationToken ct)
        {
            var fileInfo = new FileInfo(filePath);
            var ext = fileInfo.Extension.ToLowerInvariant();
            var extractor = _extractorFactory.GetExtractor(filePath);

            if (extractor == null) return;

            var pages = await extractor.ExtractAsync(filePath, ct);
            if (pages.Count == 0) return;

            var docFile = new DocumentFile
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                Extension = ext,
                FileSizeBytes = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                IndexedAt = DateTime.Now,
                FolderId = folderId,
                Status = "Indexed"
            };

            var fileId = await _databaseService.UpsertFileAsync(docFile);
            docFile.Id = fileId;

            // Chunk text
            var settings = _settingsService.Current;
            var chunks = _chunker.CreateChunks(docFile, pages, settings.ChunkWordSize, settings.ChunkWordOverlap);

            // Compute embeddings for each chunk
            foreach (var chunk in chunks)
            {
                if (ct.IsCancellationRequested) break;
                chunk.Embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, ct);
            }

            // Save chunks and FTS
            await _databaseService.InsertChunksAsync(chunks);

            docFile.ChunkCount = chunks.Count;
            await _databaseService.UpsertFileAsync(docFile);
        }

        private List<string> DiscoverFiles(string rootPath)
        {
            var files = new List<string>();
            try
            {
                DiscoverFilesRecursive(new DirectoryInfo(rootPath), files);
            }
            catch
            {
                // Access denied or unreadable directory handled gracefully
            }
            return files;
        }

        private void DiscoverFilesRecursive(DirectoryInfo dir, List<string> collected)
        {
            var settings = _settingsService.Current;

            // Check excluded directory names
            foreach (var exc in settings.ExcludedFolderNames)
            {
                if (string.Equals(dir.Name, exc, StringComparison.OrdinalIgnoreCase)) return;
            }

            try
            {
                foreach (var file in dir.GetFiles())
                {
                    if (IsExcluded(file.FullName)) continue;
                    if (_extractorFactory.IsSupported(file.FullName))
                    {
                        collected.Add(file.FullName);
                    }
                }

                foreach (var subDir in dir.GetDirectories())
                {
                    DiscoverFilesRecursive(subDir, collected);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip restricted folders
            }
        }

        private bool IsExcluded(string filePath)
        {
            var settings = _settingsService.Current;
            var ext = Path.GetExtension(filePath);

            foreach (var excExt in settings.ExcludedExtensions)
            {
                if (string.Equals(ext, excExt, StringComparison.OrdinalIgnoreCase)) return true;
            }

            foreach (var excDir in settings.ExcludedFolderNames)
            {
                if (filePath.IndexOf(Path.DirectorySeparatorChar + excDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
