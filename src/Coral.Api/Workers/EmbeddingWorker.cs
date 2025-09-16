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
    private readonly IDbContextFactory<CoralDbContext> _dbContextFactory;
    private readonly ILogger<EmbeddingWorker> _logger;

    public EmbeddingWorker(EssentiaService essentia, IEmbeddingChannel channel, IDbContextFactory<CoralDbContext> dbContextFactory, ILogger<EmbeddingWorker> logger)
    {
        _essentia = essentia;
        _channel = channel;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding worker started!");
        while (!stoppingToken.IsCancellationRequested)
        {
            await foreach (var track in _channel.GetReader().ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Processing track: {FilePath}", track.AudioFile.FilePath);
                    await using var context = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
                    _essentia.LoadAudio(track.AudioFile.FilePath);
                    var embeddings = _essentia.RunInference();
                    await context.TrackEmbeddings.AddAsync(new TrackEmbedding()
                    {
                        CreatedAt = DateTime.UtcNow,
                        Embedding = new Vector(embeddings),
                        TrackId = track.Id
                    }, stoppingToken);
                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Stored embeddings for track: {FilePath}", track.AudioFile.FilePath);
                }
                catch (EssentiaException e)
                {
                    _logger.LogError(e, "Failed to get embeddings for track: {Reason}", e.Message);
                }
            }
        }
    }
}