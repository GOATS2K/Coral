namespace Coral.Dto.DatabaseModels;

public record AlbumDto
{
    public int Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public List<ArtistDto> Artists { get; set; } = default!;
    public List<TrackDto> Tracks { get; set; } = default!;
    public List<GenreDto> Genres { get; set; } = default!;
    public int ReleaseYear { get; set; } = default!;
    public bool CoverPresent { get; set; } = false;
}