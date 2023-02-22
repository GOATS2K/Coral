namespace Coral.Dto.Models;

public record AlbumDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public List<ArtistDto> Artists { get; set; } = null!;
    public List<TrackDto> Tracks { get; set; } = null!;
    public List<GenreDto> Genres { get; set; } = null!;
    public int ReleaseYear { get; set; }
}