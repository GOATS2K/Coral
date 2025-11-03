using System.ComponentModel.DataAnnotations.Schema;
// using Pgvector;  // Commented out - moving to DuckDB

namespace Coral.Database.Models;

public class TrackEmbedding : BaseTable
{
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    // Temporary: Changed from Vector to string for SQLite compatibility
    // This table will be removed when DuckDB embedding service is implemented (Phase 9)
    // [Column(TypeName = "vector(1280)")]
    public string Embedding { get; set; } = string.Empty;  // Was: Vector Embedding
}