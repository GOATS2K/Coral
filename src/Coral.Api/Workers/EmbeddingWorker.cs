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
    private SemaphoreSlim _semaphore;

    public EmbeddingWorker(
        IEmbeddingChannel channel,
        ILogger<EmbeddingWorker> logger,
        IServiceScopeFactory scopeFactory,
        InferenceService inferenceService,
        IEmbeddingService embeddingService,
        IOptions<ServerConfiguration> config)
    {
        _channel = channel;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _inferenceService = inferenceService;
        _embeddingService = embeddingService;
        _semaphore = new SemaphoreSlim(config.Value.Inference.MaxConcurrentInstances);
    }

    /// <summary>
    /// Updates the concurrency limit. Called by OnboardingController.
    /// In-flight operations will continue using the previous semaphore until they complete.
    /// </summary>
    public void UpdateConcurrency(int maxConcurrentInstances)
    {
        _logger.LogInformation("Updating inference concurrency to {Count}", maxConcurrentInstances);
        _semaphore = new SemaphoreSlim(maxConcurrentInstances);
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

        var semaphore = _semaphore;
        await semaphore.WaitAsync(stoppingToken);
        try
        {
            switch (track.DurationInSeconds)
            {
                case < 60:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too short.",
                        track.AudioFile.FilePath);
                    return;
                case > 60 * 15:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too long.",
                        track.AudioFile.FilePath);
                    return;
            }

            if (await _embeddingService.HasEmbeddingAsync(track.Id))
            {
                _logger.LogDebug("Embedding already exists for track {FilePath}", track.AudioFile.FilePath);
                return;
            }

            if (await _embeddingService.HasFailedEmbeddingAsync(track.Id))
            {
                _logger.LogDebug("Skipping previously failed embedding for track {FilePath}", track.AudioFile.FilePath);
                return;
            }

            var embeddings = await _inferenceService.RunInference(track.AudioFile.FilePath);
            await _embeddingService.InsertEmbeddingAsync(track.Id, embeddings);

            _logger.LogInformation("Stored embeddings for track {FilePath} in {Time:F2}s",
                track.AudioFile.FilePath, sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embeddings for track: {Path}", track.AudioFile.FilePath);

            try
            {
                await _embeddingService.RecordFailedEmbeddingAsync(track.Id, ex.Message);
            }
            catch (Exception recordEx)
            {
                _logger.LogError(recordEx, "Failed to record embedding failure for track: {Path}", track.AudioFile.FilePath);
            }
        }
        finally
        {
            semaphore.Release();
            await reporter.ReportEmbeddingCompleted(job.RequestId);
        }
    }
}