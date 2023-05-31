using Coral.Database;
using Coral.Database.Models;
using Coral.Events;
using Coral.Services;
using System.Runtime.Caching;

namespace Coral.Api.Workers
{
    // https://failingfast.io/a-robust-solution-for-filesystemwatcher-firing-events-multiple-times/
    public class IndexerWorker : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<IndexerWorker> _logger;
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly MemoryCache _memCache;
        private readonly CacheItemPolicy _cacheItemPolicy;
        private const int CacheTimeMilliseconds = 250;
        private readonly MusicLibraryRegisteredEventEmitter _musicLibraryRegisteredEventEmitter;
        private readonly SemaphoreSlim _semaphore = new(1);

        public IndexerWorker(IServiceProvider serviceProvider, ILogger<IndexerWorker> logger, MusicLibraryRegisteredEventEmitter eventEmitter)
        {
            _memCache = MemoryCache.Default;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cacheItemPolicy = new CacheItemPolicy()
            {
                RemovedCallback = async (args) =>
                {
                    if (args.RemovedReason != CacheEntryRemovedReason.Expired) return;
                    await EnqueueTask(async () => {
                        var musicLibrary = GetMusicLibraryForPath(args.CacheItem.Key);
                        if (musicLibrary == null)
                        {
                            _logger.LogError("Unable to find music library for: {Path}", args.CacheItem.Key);
                            return;
                        }

                        using var scope = _serviceProvider.CreateScope();
                        var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();
                        await indexer.ScanDirectory(args.CacheItem.Key, musicLibrary);
                    });
                }
            };
            _musicLibraryRegisteredEventEmitter = eventEmitter;
        }

        async Task EnqueueTask(Func<Task> func)
        {
            await _semaphore.WaitAsync();
            await func();
            _semaphore.Release();
        }

        private MusicLibrary? GetMusicLibraryForPath(string path)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

            return context.MusicLibraries.FirstOrDefault(l => path.StartsWith(l.LibraryPath));
        }

        void HandleFileSystemEvent(object source, FileSystemEventArgs e)
        {
            var parent = Directory.GetParent(e.FullPath)?.FullName!;
            var existingItem = _memCache.Get(parent) as FileSystemEventArgs;
            if (existingItem != null)
            {
                _memCache.Remove(parent);
            }
            _cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMilliseconds(CacheTimeMilliseconds);
            _memCache.Add(parent, e, _cacheItemPolicy);
        }

        void InitializeFileSystemWatcher()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

            foreach (var musicLibrary in context.MusicLibraries.ToList())
            {
                var fsWatcher = new FileSystemWatcher(musicLibrary.LibraryPath);
                fsWatcher.Changed += HandleFileSystemEvent;
                fsWatcher.Renamed += async (_, args) =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();
                    try
                    {
                        await indexer.HandleRename(args.OldFullPath, args.FullPath);
                    } catch (ArgumentException) { }
                };
                fsWatcher.Deleted += async (_, args) =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();
                    try
                    {
                        await indexer.DeleteTrack(args.FullPath);
                    }
                    catch (ArgumentException) { }
                };
                fsWatcher.IncludeSubdirectories = true;
                fsWatcher.EnableRaisingEvents = true;
                _watchers.Add(fsWatcher);

                _logger.LogInformation("Watching {Path} for changes...", musicLibrary.LibraryPath);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            InitializeFileSystemWatcher();
            _musicLibraryRegisteredEventEmitter.MusicLibraryRegisteredEvent += async (_, args) =>
            {
                _watchers.Clear();
                using var scope = _serviceProvider.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();
                await indexer.ScanLibrary(args.Library);
                InitializeFileSystemWatcher();
            };
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
