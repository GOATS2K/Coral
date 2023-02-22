namespace Coral.Dto.Models;

public record ArtistDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}