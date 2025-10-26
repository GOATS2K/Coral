using Microsoft.Extensions.Logging;

namespace Coral.BulkExtensions;

/// <summary>
/// Statistics and performance metrics for bulk insert operations.
/// </summary>
public class BulkInsertStats
{
    public Dictionary<Type, int> EntitiesInserted { get; } = new();
    public Dictionary<Type, int> EntitiesUpdated { get; } = new();
    public Dictionary<string, int> RelationshipsInserted { get; } = new(); // Key: "Track <-> Artist"

    public int TotalEntitiesInserted => EntitiesInserted.Values.Sum();
    public int TotalEntitiesUpdated => EntitiesUpdated.Values.Sum();
    public int TotalRelationshipsInserted => RelationshipsInserted.Values.Sum();

    public TimeSpan EntityInsertionTime { get; set; }
    public TimeSpan RelationshipInsertionTime { get; set; }
    public TimeSpan TotalTime { get; set; }

    public void LogSummary(ILogger? logger)
    {
        if (logger == null) return;

        logger.LogInformation(
            "Bulk insert completed: {Entities} entities, {Relationships} relationships in {Time:F2}s",
            TotalEntitiesInserted,
            TotalRelationshipsInserted,
            TotalTime.TotalSeconds);

        foreach (var (type, count) in EntitiesInserted)
        {
            logger.LogInformation("  {Type}: {Count:N0} inserted", type.Name, count);
        }

        foreach (var (relationship, count) in RelationshipsInserted)
        {
            logger.LogInformation("  {Relationship}: {Count:N0} relationships", relationship, count);
        }
    }
}
