using Coral.Database;
using Coral.Services;
using Coral.Services.ChannelWrappers;
using Coral.Services.Indexer;

namespace Coral.Api.Workers;

public class ScanWorker : BackgroundService
{
    private readonly IScanChannel _scanChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanWorker> _logger;

    public ScanWorker(
        IScanChannel scanChannel,
        IServiceScopeFactory scopeFactory,
        ILogger<ScanWorker> logger)
    {
        _scanChannel = scanChannel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScanWorker started!");

        await foreach (var job in _scanChannel.GetReader().ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation(
                    "Received scan job for library {Library} (Trigger: {Trigger}, RequestId: {RequestId})",
                    job.Library.LibraryPath,
                    job.Trigger,
                    job.RequestId?.ToString() ?? "none");

                await ProcessScan(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scan job for library {Library}", job.Library.LibraryPath);
            }
        }

        _logger.LogWarning("ScanWorker stopped!");
    }

    private async Task ProcessScan(ScanJob job, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IDirectoryScanner>();
        var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();
        var reporter = scope.ServiceProvider.GetRequiredService<IScanReporter>();
        var embeddingChannel = scope.ServiceProvider.GetRequiredService<IEmbeddingChannel>();
        await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

        var library = await context.MusicLibraries.FindAsync(job.Library.Id);
        if (library == null)
        {
            _logger.LogError("Library {LibraryId} not found", job.Library.Id);
            return;
        }

        var expectedTracks = await scanner.CountFiles(library, job.Incremental);
        _logger.LogInformation("Expecting {ExpectedTracks} tracks to process", expectedTracks);

        reporter.RegisterScan(job.RequestId, expectedTracks, library);

        // If no tracks need processing, complete immediately
        if (expectedTracks == 0)
        {
            _logger.LogInformation("No tracks to process for {Directory}, completing scan immediately", library.LibraryPath);
            await reporter.CompleteScan(job.RequestId);
            return;
        }

        var directoryGroups = scanner.ScanLibrary(library, job.Incremental);
        var indexEvents = indexer.IndexDirectoryGroups(directoryGroups, library, cancellationToken);

        var hasCreateEvents = false;
        await foreach (var indexEvent in indexEvents)
        {
            await reporter.ReportIndexOperation(job.RequestId, indexEvent);
            if (indexEvent is { Operation: IndexerOperation.Create, Track: not null})
            {
                hasCreateEvents = true;
                await embeddingChannel.GetWriter().WriteAsync(new EmbeddingJob(indexEvent.Track, job.RequestId), cancellationToken);
            }
        }

        await indexer.FinalizeIndexing(library, cancellationToken);

        _logger.LogInformation("Completed indexing of {Directory}", library.LibraryPath);

        // If no Create events occurred, no embeddings will be generated, so complete immediately
        if (!hasCreateEvents)
        {
            _logger.LogInformation("No new tracks created for {Directory}, completing scan immediately", library.LibraryPath);
            await reporter.CompleteScan(job.RequestId);
        }
        // Otherwise, scan will auto-complete when all embeddings are processed (EmbeddingsCompleted == ExpectedTracks)
    }
}
