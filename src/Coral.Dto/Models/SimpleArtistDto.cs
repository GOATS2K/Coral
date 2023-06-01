namespace Coral.Dto.Models
{
    public record SimpleArtistDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
