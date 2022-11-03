namespace Coral.Dto.Models;

public record ArtistDto
{
    public int Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}