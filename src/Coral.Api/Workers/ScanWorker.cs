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
            IScanReporter? reporter = null;
            try
            {
                _logger.LogInformation(
                    "Received scan job for library {Library} (Trigger: {Trigger}, RequestId: {RequestId})",
                    job.Library.LibraryPath,
                    job.Trigger,
                    job.RequestId?.ToString() ?? "none");

                await using var scope = _scopeFactory.CreateAsyncScope();
                reporter = scope.ServiceProvider.GetRequiredService<IScanReporter>();

                await ProcessScan(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scan job for library {Library}", job.Library.LibraryPath);

                if (reporter != null && job.RequestId != null)
                {
                    await reporter.FailScan(job.RequestId, ex.Message);
                }
            }
        }

        _logger.LogWarning("ScanWorker stopped!");
    }

    private async Task ProcessScan(ScanJob job, CancellationToken cancellationToken)
    {
        // Dispatch to appropriate handler based on scan type
        switch (job.Type)
        {
            case ScanType.Index:
                await ProcessIndexScan(job, cancellationToken);
                break;
            case ScanType.Rename:
                await ProcessRenameScan(job, cancellationToken);
                break;
            default:
                _logger.LogError("Unknown scan type {ScanType} for library {Library}", job.Type, job.Library.LibraryPath);
                break;
        }
    }

    private async Task ProcessIndexScan(ScanJob job, CancellationToken cancellationToken)
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

        // Always run the indexing process even if expectedTracks == 0
        // because DeleteMissingTracks needs to run to clean up orphaned tracks
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

    private async Task ProcessRenameScan(ScanJob job, CancellationToken cancellationToken)
    {
        if (job.Renames == null || !job.Renames.Any())
        {
            _logger.LogWarning("ProcessRenameScan called without any renames for library {Library}", job.Library.LibraryPath);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();
        var reporter = scope.ServiceProvider.GetRequiredService<IScanReporter>();

        _logger.LogInformation("Processing {Count} rename operations for library {Library}",
            job.Renames.Count, job.Library.LibraryPath);

        // Register scan with reporter (no expected tracks for renames)
        reporter.RegisterScan(job.RequestId, 0, job.Library);

        // Process each rename operation
        foreach (var rename in job.Renames)
        {
            try
            {
                await indexer.HandleRename(rename.OldPath, rename.NewPath);
                _logger.LogInformation("Successfully renamed {OldPath} to {NewPath}", rename.OldPath, rename.NewPath);

                // Report rename operation
                await reporter.ReportIndexOperation(job.RequestId, new IndexEvent(
                    IndexerOperation.Update,
                    rename.NewPath,
                    null  // Track will be populated by indexer if needed
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rename {OldPath} to {NewPath}", rename.OldPath, rename.NewPath);
                // Continue with other renames even if one fails
            }
        }

        // Complete the scan immediately (no embeddings needed for renames)
        await reporter.CompleteScan(job.RequestId);
        _logger.LogInformation("Completed processing {Count} renames for library {Library}",
            job.Renames.Count, job.Library.LibraryPath);
    }
}
