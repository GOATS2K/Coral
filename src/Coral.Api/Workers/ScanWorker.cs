using Coral.Database;
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
                    job.RequestId ?? "none");

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
        var indexer = scope.ServiceProvider.GetRequiredService<INewIndexerService>();
        var reporter = scope.ServiceProvider.GetRequiredService<IScanReporter>();
        var embeddingChannel = scope.ServiceProvider.GetRequiredService<IEmbeddingChannel>();
        await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

        var library = await context.MusicLibraries.FindAsync(job.Library.Id);
        if (library == null)
        {
            _logger.LogError("Library {LibraryId} not found", job.Library.Id);
            return;
        }

        var expectedTracks = scanner.CountFiles(library, job.Incremental);
        _logger.LogInformation("Expecting {ExpectedTracks} tracks to process", expectedTracks);

        reporter.RegisterScan(job.RequestId, expectedTracks, library);

        var directoryGroups = scanner.ScanLibrary(library, job.Incremental);
        var tracks = indexer.IndexDirectoryGroups(directoryGroups, library, cancellationToken);

        await foreach (var track in tracks.WithCancellation(cancellationToken))
        {
            await reporter.ReportTrackIndexed(job.RequestId);
            await embeddingChannel.GetWriter().WriteAsync(new EmbeddingJob(track, job.RequestId), cancellationToken);
        }

        await indexer.FinalizeIndexing(library, cancellationToken);

        _logger.LogInformation("Completed scan of {Directory}", library.LibraryPath);
    }
}
