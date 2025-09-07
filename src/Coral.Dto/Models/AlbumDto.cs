namespace Coral.Dto.Models;

public record AlbumDto : SimpleAlbumDto
{
    public List<SimpleTrackDto> Tracks { get; set; } = null!;
    public List<GenreDto> Genres { get; set; } = null!;
}