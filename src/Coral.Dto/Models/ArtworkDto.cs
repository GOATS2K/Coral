namespace Coral.Dto.Models;

public record ArtworkDto
{
    public string Small { get; set; } = null!;
    public string Medium { get; set; } = null!;
    public string Original { get; set; } = null!;
    public string[] Colors { get; set; } = null!;

};