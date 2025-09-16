namespace Coral.Dto.Models
{
    public class MusicLibraryDto
    {
        public Guid Id { get; set; }
        public string LibraryPath { get; set; } = null!;
        public DateTime LastScan { get; set; }
    }
}
