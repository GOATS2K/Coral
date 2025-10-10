namespace Coral.Configuration
{
    public static class ApplicationConfiguration
    {
        public static string HLSDirectory { get; } = Path.Combine(Path.GetTempPath(), "CoralHLS");
        public static string AppData { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coral");
        public static string Thumbnails { get; } = Path.Combine(AppData, "Thumbnails");
        public static string ExtractedArtwork { get;  } = Path.Combine(AppData, "Extracted Artwork");
        public static string Plugins { get; } = Path.Combine(AppData, "Plugins");
        public static string Models { get; } = Path.Combine(AppData, "Models");

        public static void EnsureDirectoriesAreCreated()
        {
            Directory.CreateDirectory(AppData);
            Directory.CreateDirectory(HLSDirectory);
            Directory.CreateDirectory(Thumbnails);
            Directory.CreateDirectory(ExtractedArtwork);
            Directory.CreateDirectory(Plugins);
            Directory.CreateDirectory(Models);
        }
    }
}