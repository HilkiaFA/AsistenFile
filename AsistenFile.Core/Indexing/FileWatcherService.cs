using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AsistenFile.Core.Storage;

namespace AsistenFile.Core.Indexing
{
    public interface IFileWatcherService : IDisposable
    {
        Task StartWatchingAsync();
        void StopWatching();
        Task RefreshWatchersAsync();
    }

    public class FileWatcherService : IFileWatcherService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IIndexingService _indexingService;
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly ConcurrentDictionary<string, System.Threading.Timer> _debounceTimers = new();
        private readonly object _lock = new();

        public FileWatcherService(IDatabaseService databaseService, IIndexingService indexingService)
        {
            _databaseService = databaseService;
            _indexingService = indexingService;
        }

        public async Task StartWatchingAsync()
        {
            await RefreshWatchersAsync();
        }

        public void StopWatching()
        {
            lock (_lock)
            {
                foreach (var watcher in _watchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _watchers.Clear();

                foreach (var timer in _debounceTimers.Values)
                {
                    timer.Dispose();
                }
                _debounceTimers.Clear();
            }
        }

        public async Task RefreshWatchersAsync()
        {
            StopWatching();

            var folders = await _databaseService.GetFoldersAsync();
            lock (_lock)
            {
                foreach (var folder in folders)
                {
                    if (!folder.IsActive || !Directory.Exists(folder.FolderPath)) continue;

                    try
                    {
                        var watcher = new FileSystemWatcher(folder.FolderPath)
                        {
                            IncludeSubdirectories = true,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                        };

                        watcher.Created += OnFileChanged;
                        watcher.Changed += OnFileChanged;
                        watcher.Deleted += OnFileDeleted;
                        watcher.Renamed += OnFileRenamed;

                        watcher.EnableRaisingEvents = true;
                        _watchers.Add(watcher);
                    }
                    catch
                    {
                        // Ignore watcher registration failures on special virtual directories
                    }
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            DebounceFileAction(e.FullPath, async () =>
            {
                try
                {
                    await _indexingService.IndexSingleFileAsync(e.FullPath);
                }
                catch
                {
                    // Background watcher shouldn't crash app
                }
            });
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            DebounceFileAction(e.FullPath, async () =>
            {
                try
                {
                    await _indexingService.RemoveFileAsync(e.FullPath);
                }
                catch
                {
                    // Background watcher shouldn't crash app
                }
            });
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            DebounceFileAction(e.OldFullPath, async () =>
            {
                try
                {
                    await _indexingService.RemoveFileAsync(e.OldFullPath);
                    await _indexingService.IndexSingleFileAsync(e.FullPath);
                }
                catch
                {
                    // Background watcher shouldn't crash app
                }
            });
        }

        private void DebounceFileAction(string filePath, Func<Task> action)
        {
            // Wait 1.5 seconds for file stream lock to be released by saving application
            if (_debounceTimers.TryGetValue(filePath, out var existingTimer))
            {
                try
                {
                    existingTimer.Change(1500, Timeout.Infinite);
                    return;
                }
                catch (ObjectDisposedException)
                {
                    // Timer was already disposed, recreate below
                }
            }

            var timer = new System.Threading.Timer(_ =>
            {
                if (_debounceTimers.TryRemove(filePath, out var t))
                {
                    t.Dispose();
                }
                _ = action();
            }, null, 1500, Timeout.Infinite);

            _debounceTimers[filePath] = timer;
        }

        public void Dispose()
        {
            StopWatching();
        }
    }
}
