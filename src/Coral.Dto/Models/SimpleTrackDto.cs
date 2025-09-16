namespace Coral.Dto.Models;

public class SimpleTrackDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = null!;
    public int DurationInSeconds { get; set; }
    public int TrackNumber { get; set; }
    public int DiscNumber { get; set; }
    public List<ArtistWithRoleDto> Artists { get; set; } = null!;
    public GenreDto? Genre { get; init; }
}