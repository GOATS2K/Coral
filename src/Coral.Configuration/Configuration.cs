using System.Dynamic;

namespace Coral.Configuration
{
    public static class ApplicationConfiguration
    {
        public static string HLSDirectory { get; } = Path.Join(Path.GetTempPath(), "CoralHLS");
        public static string AppData { get; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coral");
        public static string Thumbnails { get; } = Path.Join(AppData, "thumbnails");
        public static string ExtractedArtwork { get;  } = Path.Join(AppData, "extracted_artwork");
    }
}