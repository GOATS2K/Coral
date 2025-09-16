namespace Coral.Dto.Models
{
    public record SimpleArtistDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
