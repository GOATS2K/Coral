using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Coral.Database.Models;

public class TrackEmbedding : BaseTable
{
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;
    
    [Column(TypeName = "vector(1280)")] 
    public Vector Embedding { get; set; } = null!;
}