using Coral.Database.Models;

namespace Coral.Dto.Models;

public record AlbumDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public AlbumType? Type { get; set; }
    public List<SimpleArtistDto> Artists { get; set; } = null!;
    public List<TrackDto> Tracks { get; set; } = null!;
    public List<GenreDto> Genres { get; set; } = null!;
    public int ReleaseYear { get; set; }
}