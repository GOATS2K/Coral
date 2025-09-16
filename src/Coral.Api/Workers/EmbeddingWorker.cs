using System.Diagnostics;
using Coral.Database;
using Coral.Database.Models;
using Coral.Essentia.Bindings;
using Coral.Services.ChannelWrappers;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Coral.Api.Workers;

public class EmbeddingWorker : BackgroundService
{
    private readonly EssentiaService _essentia;
    private readonly IEmbeddingChannel _channel;
    private readonly ILogger<EmbeddingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public EmbeddingWorker(EssentiaService essentia, IEmbeddingChannel channel, ILogger<EmbeddingWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _essentia = essentia;
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
                await GetEmbeddings(stoppingToken, track);
            }
        }
        _logger.LogWarning("Embedding worker stopped!");
    }

    private async Task GetEmbeddings(CancellationToken stoppingToken, Track track)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            switch (track.DurationInSeconds)
            {
                case < 60:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too short.", track.AudioFile.FilePath);
                    return;
                // if the track is longer than 15 minutes, it's probably a podcast/radio show/mix
                case > 60 * 15:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too long.", track.AudioFile.FilePath);
                    return;
            }

            _logger.LogInformation("Processing track: {FilePath}", track.AudioFile.FilePath);
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
            _essentia.LoadAudio(track.AudioFile.FilePath);
            _logger.LogInformation("Loaded track: {FilePath} in {Time} seconds", track.AudioFile.FilePath,  sw.Elapsed.TotalSeconds);
            var embeddings = _essentia.RunInference();
            _logger.LogInformation("Got embeddings for track: {FilePath} in {Time} seconds", track.AudioFile.FilePath,  sw.Elapsed.TotalSeconds);
            await context.TrackEmbeddings.AddAsync(new TrackEmbedding()
            {
                CreatedAt = DateTime.UtcNow,
                Embedding = new Vector(embeddings),
                TrackId = track.Id
            }, stoppingToken);
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Stored embeddings for track {FilePath} in {Time} seconds", track.AudioFile.FilePath, sw.Elapsed.TotalSeconds);
        }
        catch (EssentiaException e)
        {
            _logger.LogError(e, "Failed to get embeddings for track: {Reason}", e.Message);
        }
    }
}