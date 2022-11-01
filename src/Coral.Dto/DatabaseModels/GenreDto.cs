namespace Coral.Dto.DatabaseModels;

public record GenreDto
{
    public int Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}