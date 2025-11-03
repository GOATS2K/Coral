using Coral.Configuration;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;

namespace Coral.Services;

public interface IEmbeddingService
{
    Task InitializeAsync();
    Task InsertEmbeddingAsync(Guid trackId, float[] embedding);
    Task<bool> HasEmbeddingAsync(Guid trackId);
    Task<List<(Guid TrackId, double Distance)>> GetSimilarTracksAsync(
        Guid trackId, int limit = 100, double maxDistance = 0.2);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly string _connectionString;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(ILogger<EmbeddingService> logger)
    {
        _connectionString = $"Data Source={ApplicationConfiguration.DuckDbEmbeddingsPath}";
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        // Install and load VSS extension
        _logger.LogInformation("Loading DuckDB VSS extension...");
        command.CommandText = "INSTALL vss; LOAD vss;";
        await command.ExecuteNonQueryAsync();

        // Create embeddings table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS track_embeddings (
                track_id UUID PRIMARY KEY,
                embedding FLOAT[1280],
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )";
        await command.ExecuteNonQueryAsync();

        // Check if we have data
        command.CommandText = "SELECT COUNT(*) FROM track_embeddings";
        var count = (long)await command.ExecuteScalarAsync()!;

        _logger.LogInformation("DuckDB initialized with {Count} embeddings", count);

        // Create HNSW index if we have enough data (>1000 rows recommended)
        if (count > 1000)
        {
            _logger.LogInformation("Creating HNSW index on {Count} embeddings...", count);
            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS track_embeddings_hnsw_idx
                ON track_embeddings
                USING HNSW(embedding)
                WITH (metric = 'cosine')";
            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("HNSW index created successfully");
        }
        else if (count > 0)
        {
            _logger.LogInformation(
                "Skipping HNSW index creation (need >1000 rows, have {Count})", count);
        }
    }

    public async Task InsertEmbeddingAsync(Guid trackId, float[] embedding)
    {
        if (embedding.Length != 1280)
            throw new ArgumentException(
                $"Expected 1280-dimensional embedding, got {embedding.Length}");

        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        // Use INSERT OR REPLACE for upsert behavior
        command.CommandText = @"
            INSERT OR REPLACE INTO track_embeddings (track_id, embedding, created_at)
            VALUES ($1, $2, CURRENT_TIMESTAMP)";

        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));
        command.Parameters.Add(new DuckDBParameter(embedding));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasEmbeddingAsync(Guid trackId)
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) FROM track_embeddings WHERE track_id = $1";
        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));

        var count = (long)await command.ExecuteScalarAsync()!;
        return count > 0;
    }

    public async Task<List<(Guid TrackId, double Distance)>> GetSimilarTracksAsync(
        Guid trackId, int limit = 100, double maxDistance = 0.2)
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        // First, get the embedding for the source track
        command.CommandText = @"
            SELECT embedding FROM track_embeddings WHERE track_id = $1";
        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));

        var result = await command.ExecuteScalarAsync();
        if (result == null)
        {
            _logger.LogWarning("No embedding found for track {TrackId}", trackId);
            return new List<(Guid, double)>();
        }

        var sourceEmbedding = (float[])result;

        // Find similar tracks using array_cosine_distance
        // Note: If HNSW index exists, DuckDB will use it automatically
        command.CommandText = @"
            SELECT
                track_id,
                array_cosine_distance(embedding, $1::FLOAT[1280]) AS distance
            FROM track_embeddings
            WHERE track_id != $2
            ORDER BY distance ASC
            LIMIT $3";

        command.Parameters.Clear();
        command.Parameters.Add(new DuckDBParameter(sourceEmbedding));
        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));
        command.Parameters.Add(new DuckDBParameter(limit));

        var results = new List<(Guid, double)>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader.GetString(0));
            var distance = reader.GetDouble(1);

            // Filter by max distance
            if (distance <= maxDistance)
            {
                results.Add((id, distance));
            }
        }

        return results;
    }
}
