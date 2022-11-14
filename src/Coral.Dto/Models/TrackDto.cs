namespace Coral.Dto.Models;

public record TrackDto
{
    public int Id { get; init; }
    public string Title { get; init; } = default!;
    public int DurationInSeconds { get; set; } = default!;
    public string? Comment { get; set; } = default!;
    public int TrackNumber { get; set; } = default!;
    public int DiscNumber { get; set; } = default!;
    public SimpleArtistDto Artist { get; init; } = default!;
    public SimpleAlbumDto Album { get; init; } = default!;
    public GenreDto? Genre { get; init; }

}