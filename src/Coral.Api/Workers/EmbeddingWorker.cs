using System.Diagnostics;
using Coral.Database;
using Coral.Database.Models;
using Coral.Services.ChannelWrappers;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Coral.Api.Workers;

public class EmbeddingWorker : BackgroundService
{
    private readonly IEmbeddingChannel _channel;
    private readonly ILogger<EmbeddingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _modelPath = @"P:\discogs_embeddings_both_outputs.onnx";
    private readonly SemaphoreSlim _semaphore = new(10);

    public EmbeddingWorker(IEmbeddingChannel channel, ILogger<EmbeddingWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _channel = channel;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding worker started!");
        while (!stoppingToken.IsCancellationRequested)
        {
            await foreach (var track in _channel.GetReader().ReadAllAsync(stoppingToken))
            {
                _ = Task.Run(async () => await GetEmbeddings(stoppingToken, track), stoppingToken);
            }
        }

        _logger.LogWarning("Embedding worker stopped!");
    }

    private async Task GetEmbeddings(CancellationToken stoppingToken, Track track)
    {
        var sw = Stopwatch.StartNew();
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
            using var essentia = new EssentiaInference();
            essentia.LoadModel(_modelPath);
            var embeddings = essentia.RunInference(track.AudioFile.FilePath);
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
            await context.TrackEmbeddings.AddAsync(new TrackEmbedding()
            {
                CreatedAt = DateTime.UtcNow,
                Embedding = new Vector(embeddings),
                TrackId = track.Id
            }, stoppingToken);
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Stored embeddings for track {FilePath} in {Time} seconds",
                track.AudioFile.FilePath, sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embeddings for track: {Path}", track.AudioFile.FilePath);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}