namespace Coral.Dto.DatabaseModels;

public record ArtistDto
{
    public int Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}