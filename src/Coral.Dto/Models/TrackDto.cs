namespace Coral.Dto.Models;

public record TrackDto
{
    public int Id { get; init; }
    public string Title { get; init; } = null!;
    public int DurationInSeconds { get; set; }
    public string? Comment { get; set; }
    public int TrackNumber { get; set; }
    public int DiscNumber { get; set; }
    public List<ArtistWithRoleDto> Artists { get; set; } = null!;
    public SimpleAlbumDto Album { get; init; } = null!;
    public GenreDto? Genre { get; init; }

}