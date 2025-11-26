namespace Coral.Dto.Models;

public record PlaylistDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<PlaylistTrackDto> Tracks { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record PlaylistTrackDto
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; }
    public SimpleTrackDto Track { get; set; } = null!;
}
