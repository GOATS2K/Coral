namespace Coral.Dto.Models;

public record GenreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}