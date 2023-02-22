namespace Coral.Dto.Models;

public record TrackDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = null!;
    public int DurationInSeconds { get; set; }
    public string? Comment { get; set; }
    public int TrackNumber { get; set; }
    public int DiscNumber { get; set; }
    public SimpleArtistDto Artist { get; init; } = null!;
    public SimpleAlbumDto Album { get; init; } = null!;
    public GenreDto? Genre { get; init; }

}