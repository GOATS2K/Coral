using System.Diagnostics;
using Coral.Configuration.Models;
using Coral.Database;
using Coral.Services;
using Coral.Services.ChannelWrappers;
using Microsoft.Extensions.Options;

namespace Coral.Api.Workers;

public class EmbeddingWorker : BackgroundService
{
    private readonly IEmbeddingChannel _channel;
    private readonly ILogger<EmbeddingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InferenceService _inferenceService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDisposable? _optionsChangeListener;
    private SemaphoreSlim _semaphore;

    public EmbeddingWorker(
        IEmbeddingChannel channel,
        ILogger<EmbeddingWorker> logger,
        IServiceScopeFactory scopeFactory,
        InferenceService inferenceService,
        IEmbeddingService embeddingService,
        IOptionsMonitor<ServerConfiguration> config)
    {
        _channel = channel;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _inferenceService = inferenceService;
        _embeddingService = embeddingService;
        _semaphore = new SemaphoreSlim(config.CurrentValue.Inference.MaxConcurrentInstances);

        _optionsChangeListener = config.OnChange(newConfig =>
        {
            var newCount = newConfig.Inference.MaxConcurrentInstances;
            _logger.LogInformation("Inference concurrency changed to {Count}", newCount);
            _semaphore = new SemaphoreSlim(newCount);
        });
    }

    public override void Dispose()
    {
        _optionsChangeListener?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding worker started!");

        await _inferenceService.EnsureModelExists();

        while (!stoppingToken.IsCancellationRequested)
        {
            await foreach (var job in _channel.GetReader().ReadAllAsync(stoppingToken))
            {
                _ = Task.Run(async () => await GetEmbeddings(stoppingToken, job), stoppingToken);
            }
        }

        _logger.LogWarning("Embedding worker stopped!");
    }

    private async Task GetEmbeddings(CancellationToken stoppingToken, EmbeddingJob job)
    {
        var track = job.Track;
        var sw = Stopwatch.StartNew();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var reporter = scope.ServiceProvider.GetRequiredService<IScanReporter>();

        try
        {
            switch (track.DurationInSeconds)
            {
                case < 60:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too short.",
                        track.AudioFile.FilePath);
                    return;
                // if the track is longer than 15 minutes, it's probably a podcast/radio show/mix
                case > 60 * 15:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too long.",
                        track.AudioFile.FilePath);
                    return;
            }

            await _semaphore.WaitAsync(stoppingToken);
            try
            {
                // Check if embedding already exists in DuckDB
                if (await _embeddingService.HasEmbeddingAsync(track.Id))
                {
                    _logger.LogDebug("Embedding already exists for track {FilePath}", track.AudioFile.FilePath);
                    return;
                }

                var embeddings = await _inferenceService.RunInference(track.AudioFile.FilePath);

                // Store embedding in DuckDB
                await _embeddingService.InsertEmbeddingAsync(track.Id, embeddings);

                _logger.LogInformation("Stored embeddings for track {FilePath} in {Time:F2}s",
                    track.AudioFile.FilePath, sw.Elapsed.TotalSeconds);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embeddings for track: {Path}", track.AudioFile.FilePath);
        }
        finally
        {
            // Always report embedding completed (even if skipped) so scan can finish
            await reporter.ReportEmbeddingCompleted(job.RequestId);
        }
    }
}