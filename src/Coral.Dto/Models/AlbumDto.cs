using Coral.Database.Models;

namespace Coral.Dto.Models;

public record AlbumDto : SimpleAlbumDto
{
    public List<TrackDto> Tracks { get; set; } = null!;
    public List<GenreDto> Genres { get; set; } = null!;
}