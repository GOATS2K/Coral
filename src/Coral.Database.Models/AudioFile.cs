namespace Coral.Database.Models
{
    public class AudioFile : BaseTable
    {
        public string FilePath { get; set; } = null!;
        public decimal FileSizeInBytes { get; set; }
        public Guid AudioMetadataId { get; set; }
        public AudioMetadata AudioMetadata { get; set; } = null!;
        public Guid LibraryId { get; set; }
        public MusicLibrary Library { get; set; } = null!;
    }
}
