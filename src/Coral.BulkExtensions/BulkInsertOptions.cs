using Microsoft.Extensions.Logging;

namespace Coral.BulkExtensions;

/// <summary>
/// Configuration options for bulk insert operations.
/// </summary>
public class BulkInsertOptions
{
    /// <summary>
    /// Logger for diagnostics and performance metrics.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Batch size for junction table inserts (default: 50,000).
    /// </summary>
    public int JunctionBatchSize { get; set; } = 50_000;

    /// <summary>
    /// Batch size for entity inserts (default: 10,000).
    /// </summary>
    public int EntityBatchSize { get; set; } = 10_000;

    /// <summary>
    /// Whether to disable auto-detect changes during bulk operations (default: true).
    /// Improves performance but requires manual tracking.
    /// </summary>
    public bool DisableAutoDetectChanges { get; set; } = true;
}
