using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Models;
using Microsoft.Data.Sqlite;

namespace AsistenFile.Core.Storage
{
    public interface IDatabaseService
    {
        string DatabasePath { get; }
        Task InitializeAsync();
        
        // Folders
        Task<List<IndexedFolder>> GetFoldersAsync();
        Task<IndexedFolder> AddFolderAsync(string folderPath);
        Task RemoveFolderAsync(long folderId);
        Task UpdateFolderScanTimeAsync(long folderId, DateTime scanTime, int fileCount);

        // Files
        Task<DocumentFile?> GetFileByPathAsync(string filePath);
        Task<List<DocumentFile>> GetAllFilesAsync();
        Task<long> UpsertFileAsync(DocumentFile file);
        Task DeleteFileAndChunksAsync(string filePath);

        // Chunks
        Task InsertChunksAsync(List<DocumentChunk> chunks);
        Task<List<DocumentChunk>> SearchFtsAsync(string query, int limit = 20, DateTime? from = null, DateTime? to = null, string? ext = null);
        Task<List<DocumentChunk>> GetAllChunksForVectorSearchAsync(DateTime? from = null, DateTime? to = null, string? ext = null);
        Task<int> GetTotalIndexedChunksCountAsync();
        Task<int> GetTotalIndexedFilesCountAsync();
    }

    public class SqliteDatabaseService : IDatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public string DatabasePath => _dbPath;

        public SqliteDatabaseService(string? customDbPath = null)
        {
            if (string.IsNullOrEmpty(customDbPath))
            {
                var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AsistenFile");
                Directory.CreateDirectory(appDir);
                _dbPath = Path.Combine(appDir, "asisten_file.db");
            }
            else
            {
                var dir = Path.GetDirectoryName(customDbPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                _dbPath = customDbPath;
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();
        }

        public async Task InitializeAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                // Enable WAL mode for high concurrency & speed
                using (var walCmd = conn.CreateCommand())
                {
                    walCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                    await walCmd.ExecuteNonQueryAsync();
                }

                // Create Folders table
                var createFoldersSql = @"
                    CREATE TABLE IF NOT EXISTS Folders (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FolderPath TEXT NOT NULL UNIQUE,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        AddedAt TEXT NOT NULL,
                        LastScannedAt TEXT,
                        FileCount INTEGER NOT NULL DEFAULT 0
                    );";
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = createFoldersSql;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Create Files table
                var createFilesSql = @"
                    CREATE TABLE IF NOT EXISTS Files (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FilePath TEXT NOT NULL UNIQUE,
                        FileName TEXT NOT NULL,
                        Extension TEXT NOT NULL,
                        FileSizeBytes INTEGER NOT NULL,
                        LastModified TEXT NOT NULL,
                        IndexedAt TEXT NOT NULL,
                        ChunkCount INTEGER NOT NULL DEFAULT 0,
                        Status TEXT NOT NULL DEFAULT 'Indexed',
                        ErrorMessage TEXT,
                        FolderId INTEGER NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS IX_Files_Path ON Files(FilePath);
                    CREATE INDEX IF NOT EXISTS IX_Files_Ext ON Files(Extension);
                    CREATE INDEX IF NOT EXISTS IX_Files_Modified ON Files(LastModified);";
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = createFilesSql;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Create Chunks table
                var createChunksSql = @"
                    CREATE TABLE IF NOT EXISTS Chunks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FileId INTEGER NOT NULL,
                        FilePath TEXT NOT NULL,
                        FileName TEXT NOT NULL,
                        Extension TEXT NOT NULL,
                        PageNumber INTEGER NOT NULL,
                        SectionTitle TEXT NOT NULL,
                        ChunkIndex INTEGER NOT NULL,
                        Content TEXT NOT NULL,
                        WordCount INTEGER NOT NULL,
                        Embedding BLOB,
                        FileLastModified TEXT NOT NULL,
                        FOREIGN KEY(FileId) REFERENCES Files(Id) ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS IX_Chunks_FileId ON Chunks(FileId);
                    CREATE INDEX IF NOT EXISTS IX_Chunks_Path ON Chunks(FilePath);
                    CREATE INDEX IF NOT EXISTS IX_Chunks_Modified ON Chunks(FileLastModified);";
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = createChunksSql;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Create FTS5 virtual table for lightning-fast BM25 full text search
                try
                {
                    var createFtsSql = @"
                        CREATE VIRTUAL TABLE IF NOT EXISTS Chunks_FTS USING fts5(
                            ChunkId UNINDEXED,
                            FileName,
                            SectionTitle,
                            Content,
                            tokenize = 'porter unicode61'
                        );";
                    using var cmdFts = conn.CreateCommand();
                    cmdFts.CommandText = createFtsSql;
                    await cmdFts.ExecuteNonQueryAsync();
                }
                catch
                {
                    // If FTS5 tokenizers fail on certain SQLite builds, fallback to standard fts5
                    var fallbackFts = @"
                        CREATE VIRTUAL TABLE IF NOT EXISTS Chunks_FTS USING fts5(
                            ChunkId UNINDEXED,
                            FileName,
                            SectionTitle,
                            Content
                        );";
                    using var cmdFtsFallback = conn.CreateCommand();
                    cmdFtsFallback.CommandText = fallbackFts;
                    await cmdFtsFallback.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<IndexedFolder>> GetFoldersAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var list = new List<IndexedFolder>();
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, FolderPath, IsActive, AddedAt, LastScannedAt, FileCount FROM Folders ORDER BY FolderPath ASC";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new IndexedFolder
                    {
                        Id = reader.GetInt64(0),
                        FolderPath = reader.GetString(1),
                        IsActive = reader.GetInt32(2) == 1,
                        AddedAt = DateTime.Parse(reader.GetString(3)),
                        LastScannedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                        FileCount = reader.GetInt32(5)
                    });
                }
                return list;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IndexedFolder> AddFolderAsync(string folderPath)
        {
            var normalized = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Folders (FolderPath, IsActive, AddedAt, FileCount)
                    VALUES (@p, 1, @now, 0)
                    ON CONFLICT(FolderPath) DO UPDATE SET IsActive = 1;
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@p", normalized);
                cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));

                var id = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                return new IndexedFolder
                {
                    Id = id,
                    FolderPath = normalized,
                    IsActive = true,
                    AddedAt = DateTime.Now,
                    FileCount = 0
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task RemoveFolderAsync(long folderId)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                // Find files under this folder and delete them
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM Chunks WHERE FileId IN (SELECT Id FROM Files WHERE FolderId = @fid);
                    DELETE FROM Files WHERE FolderId = @fid;
                    DELETE FROM Folders WHERE Id = @fid;";
                cmd.Parameters.AddWithValue("@fid", folderId);
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateFolderScanTimeAsync(long folderId, DateTime scanTime, int fileCount)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Folders SET LastScannedAt = @t, FileCount = @c WHERE Id = @id";
                cmd.Parameters.AddWithValue("@t", scanTime.ToString("o"));
                cmd.Parameters.AddWithValue("@c", fileCount);
                cmd.Parameters.AddWithValue("@id", folderId);
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<DocumentFile?> GetFileByPathAsync(string filePath)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, FilePath, FileName, Extension, FileSizeBytes, LastModified, IndexedAt, ChunkCount, Status, ErrorMessage, FolderId FROM Files WHERE FilePath = @p LIMIT 1";
                cmd.Parameters.AddWithValue("@p", filePath);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new DocumentFile
                    {
                        Id = reader.GetInt64(0),
                        FilePath = reader.GetString(1),
                        FileName = reader.GetString(2),
                        Extension = reader.GetString(3),
                        FileSizeBytes = reader.GetInt64(4),
                        LastModified = DateTime.Parse(reader.GetString(5)),
                        IndexedAt = DateTime.Parse(reader.GetString(6)),
                        ChunkCount = reader.GetInt32(7),
                        Status = reader.GetString(8),
                        ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
                        FolderId = reader.GetInt64(10)
                    };
                }
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<DocumentFile>> GetAllFilesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var list = new List<DocumentFile>();
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, FilePath, FileName, Extension, FileSizeBytes, LastModified, IndexedAt, ChunkCount, Status, ErrorMessage, FolderId FROM Files ORDER BY LastModified DESC";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DocumentFile
                    {
                        Id = reader.GetInt64(0),
                        FilePath = reader.GetString(1),
                        FileName = reader.GetString(2),
                        Extension = reader.GetString(3),
                        FileSizeBytes = reader.GetInt64(4),
                        LastModified = DateTime.Parse(reader.GetString(5)),
                        IndexedAt = DateTime.Parse(reader.GetString(6)),
                        ChunkCount = reader.GetInt32(7),
                        Status = reader.GetString(8),
                        ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
                        FolderId = reader.GetInt64(10)
                    });
                }
                return list;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<long> UpsertFileAsync(DocumentFile file)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Files (FilePath, FileName, Extension, FileSizeBytes, LastModified, IndexedAt, ChunkCount, Status, ErrorMessage, FolderId)
                    VALUES (@p, @n, @e, @s, @m, @i, @c, @st, @err, @fid)
                    ON CONFLICT(FilePath) DO UPDATE SET
                        FileName = excluded.FileName,
                        Extension = excluded.Extension,
                        FileSizeBytes = excluded.FileSizeBytes,
                        LastModified = excluded.LastModified,
                        IndexedAt = excluded.IndexedAt,
                        ChunkCount = excluded.ChunkCount,
                        Status = excluded.Status,
                        ErrorMessage = excluded.ErrorMessage,
                        FolderId = excluded.FolderId;
                    SELECT Id FROM Files WHERE FilePath = @p;";

                cmd.Parameters.AddWithValue("@p", file.FilePath);
                cmd.Parameters.AddWithValue("@n", file.FileName);
                cmd.Parameters.AddWithValue("@e", file.Extension);
                cmd.Parameters.AddWithValue("@s", file.FileSizeBytes);
                cmd.Parameters.AddWithValue("@m", file.LastModified.ToString("o"));
                cmd.Parameters.AddWithValue("@i", file.IndexedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@c", file.ChunkCount);
                cmd.Parameters.AddWithValue("@st", file.Status);
                cmd.Parameters.AddWithValue("@err", (object?)file.ErrorMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fid", file.FolderId);

                var id = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                file.Id = id;
                return id;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteFileAndChunksAsync(string filePath)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "DELETE FROM Chunks_FTS WHERE ChunkId IN (SELECT Id FROM Chunks WHERE FilePath = @p);";
                        cmd.Parameters.AddWithValue("@p", filePath);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "DELETE FROM Chunks WHERE FilePath = @p;";
                        cmd.Parameters.AddWithValue("@p", filePath);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "DELETE FROM Files WHERE FilePath = @p;";
                        cmd.Parameters.AddWithValue("@p", filePath);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task InsertChunksAsync(List<DocumentChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0) return;

            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();
                try
                {
                    var fileId = chunks[0].FileId;
                    var filePath = chunks[0].FilePath;

                    // Remove existing chunks for this file
                    using (var delCmd = conn.CreateCommand())
                    {
                        delCmd.Transaction = tx;
                        delCmd.CommandText = @"
                            DELETE FROM Chunks_FTS WHERE ChunkId IN (SELECT Id FROM Chunks WHERE FileId = @fid);
                            DELETE FROM Chunks WHERE FileId = @fid;";
                        delCmd.Parameters.AddWithValue("@fid", fileId);
                        await delCmd.ExecuteNonQueryAsync();
                    }

                    // Insert chunks
                    var insertChunkSql = @"
                        INSERT INTO Chunks (FileId, FilePath, FileName, Extension, PageNumber, SectionTitle, ChunkIndex, Content, WordCount, Embedding, FileLastModified)
                        VALUES (@fid, @fp, @fn, @ext, @pg, @sec, @idx, @cnt, @wc, @emb, @flm);
                        SELECT last_insert_rowid();";

                    var insertFtsSql = @"
                        INSERT INTO Chunks_FTS (ChunkId, FileName, SectionTitle, Content)
                        VALUES (@cid, @fn, @sec, @cnt);";

                    foreach (var chunk in chunks)
                    {
                        byte[]? embeddingBlob = null;
                        if (chunk.Embedding != null && chunk.Embedding.Length > 0)
                        {
                            embeddingBlob = new byte[chunk.Embedding.Length * 4];
                            Buffer.BlockCopy(chunk.Embedding, 0, embeddingBlob, 0, embeddingBlob.Length);
                        }

                        long chunkId;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = insertChunkSql;
                            cmd.Parameters.AddWithValue("@fid", chunk.FileId);
                            cmd.Parameters.AddWithValue("@fp", chunk.FilePath);
                            cmd.Parameters.AddWithValue("@fn", chunk.FileName);
                            cmd.Parameters.AddWithValue("@ext", chunk.Extension);
                            cmd.Parameters.AddWithValue("@pg", chunk.PageNumber);
                            cmd.Parameters.AddWithValue("@sec", chunk.SectionTitle);
                            cmd.Parameters.AddWithValue("@idx", chunk.ChunkIndex);
                            cmd.Parameters.AddWithValue("@cnt", chunk.Content);
                            cmd.Parameters.AddWithValue("@wc", chunk.WordCount);
                            cmd.Parameters.AddWithValue("@emb", (object?)embeddingBlob ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@flm", chunk.FileLastModified.ToString("o"));

                            chunkId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                            chunk.Id = chunkId;
                        }

                        // Index into FTS
                        using (var cmdFts = conn.CreateCommand())
                        {
                            cmdFts.Transaction = tx;
                            cmdFts.CommandText = insertFtsSql;
                            cmdFts.Parameters.AddWithValue("@cid", chunkId);
                            cmdFts.Parameters.AddWithValue("@fn", chunk.FileName);
                            cmdFts.Parameters.AddWithValue("@sec", chunk.SectionTitle);
                            cmdFts.Parameters.AddWithValue("@cnt", chunk.Content);
                            await cmdFts.ExecuteNonQueryAsync();
                        }
                    }

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<DocumentChunk>> SearchFtsAsync(string query, int limit = 20, DateTime? from = null, DateTime? to = null, string? ext = null)
        {
            var results = new List<DocumentChunk>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            // Sanitize query for FTS5 syntax
            var sanitized = CleanFtsQuery(query);
            if (string.IsNullOrWhiteSpace(sanitized)) return results;

            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    SELECT c.Id, c.FileId, c.FilePath, c.FileName, c.Extension, c.PageNumber, c.SectionTitle, c.ChunkIndex, c.Content, c.WordCount, c.Embedding, c.FileLastModified
                    FROM Chunks_FTS f
                    JOIN Chunks c ON c.Id = f.ChunkId
                    WHERE Chunks_FTS MATCH @query";

                if (from.HasValue) sql += " AND c.FileLastModified >= @from";
                if (to.HasValue) sql += " AND c.FileLastModified <= @to";
                if (!string.IsNullOrEmpty(ext)) sql += " AND c.Extension = @ext";

                sql += " ORDER BY rank LIMIT @lim";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@query", sanitized);
                cmd.Parameters.AddWithValue("@lim", limit);
                if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.ToString("o"));
                if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.ToString("o"));
                if (!string.IsNullOrEmpty(ext)) cmd.Parameters.AddWithValue("@ext", ext.StartsWith(".") ? ext : $".{ext}");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var chunk = ReadChunk(reader);
                    results.Add(chunk);
                }

                return results;
            }
            catch
            {
                // Fallback to LIKE keyword search if FTS syntax had special character issue
                return await SearchLikeFallbackAsync(query, limit, from, to, ext);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<List<DocumentChunk>> SearchLikeFallbackAsync(string query, int limit, DateTime? from, DateTime? to, string? ext)
        {
            var results = new List<DocumentChunk>();
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return results;

            var sql = @"
                SELECT Id, FileId, FilePath, FileName, Extension, PageNumber, SectionTitle, ChunkIndex, Content, WordCount, Embedding, FileLastModified
                FROM Chunks
                WHERE 1=1";

            for (int i = 0; i < words.Length; i++)
            {
                sql += $" AND (Content LIKE @w{i} OR FileName LIKE @w{i})";
            }

            if (from.HasValue) sql += " AND FileLastModified >= @from";
            if (to.HasValue) sql += " AND FileLastModified <= @to";
            if (!string.IsNullOrEmpty(ext)) sql += " AND Extension = @ext";

            sql += " ORDER BY Id DESC LIMIT @lim";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < words.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@w{i}", $"%{words[i]}%");
            }
            cmd.Parameters.AddWithValue("@lim", limit);
            if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.ToString("o"));
            if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.ToString("o"));
            if (!string.IsNullOrEmpty(ext)) cmd.Parameters.AddWithValue("@ext", ext.StartsWith(".") ? ext : $".{ext}");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(ReadChunk(reader));
            }
            return results;
        }

        public async Task<List<DocumentChunk>> GetAllChunksForVectorSearchAsync(DateTime? from = null, DateTime? to = null, string? ext = null)
        {
            await _semaphore.WaitAsync();
            try
            {
                var list = new List<DocumentChunk>();
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                var sql = "SELECT Id, FileId, FilePath, FileName, Extension, PageNumber, SectionTitle, ChunkIndex, Content, WordCount, Embedding, FileLastModified FROM Chunks WHERE 1=1";
                if (from.HasValue) sql += " AND FileLastModified >= @from";
                if (to.HasValue) sql += " AND FileLastModified <= @to";
                if (!string.IsNullOrEmpty(ext)) sql += " AND Extension = @ext";

                sql += " LIMIT 5000";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.ToString("o"));
                if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.ToString("o"));
                if (!string.IsNullOrEmpty(ext)) cmd.Parameters.AddWithValue("@ext", ext.StartsWith(".") ? ext : $".{ext}");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(ReadChunk(reader));
                }
                return list;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> GetTotalIndexedChunksCountAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Chunks";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> GetTotalIndexedFilesCountAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Files WHERE Status = 'Indexed'";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static DocumentChunk ReadChunk(SqliteDataReader reader)
        {
            var chunk = new DocumentChunk
            {
                Id = reader.GetInt64(0),
                FileId = reader.GetInt64(1),
                FilePath = reader.GetString(2),
                FileName = reader.GetString(3),
                Extension = reader.GetString(4),
                PageNumber = reader.GetInt32(5),
                SectionTitle = reader.GetString(6),
                ChunkIndex = reader.GetInt32(7),
                Content = reader.GetString(8),
                WordCount = reader.GetInt32(9),
                FileLastModified = DateTime.Parse(reader.GetString(11))
            };

            if (!reader.IsDBNull(10))
            {
                var blob = (byte[])reader[10];
                if (blob.Length > 0 && blob.Length % 4 == 0)
                {
                    var floats = new float[blob.Length / 4];
                    Buffer.BlockCopy(blob, 0, floats, 0, blob.Length);
                    chunk.Embedding = floats;
                    chunk.EmbeddingBlob = blob;
                }
            }

            return chunk;
        }

        private static string CleanFtsQuery(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var parts = input.Split(new[] { ' ', '"', '\'', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}' },
                StringSplitOptions.RemoveEmptyEntries);

            var validTerms = new List<string>();
            foreach (var p in parts)
            {
                var clean = p.Trim('*', '^', '~');
                if (clean.Length > 1)
                {
                    validTerms.Add($"\"{clean}\"*");
                }
            }

            return string.Join(" OR ", validTerms);
        }
    }
}
