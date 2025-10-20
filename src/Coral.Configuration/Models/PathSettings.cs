using System.Text.Json.Serialization;

namespace Coral.Configuration.Models;

public class PathSettings
{
    public string Data { get; set; } = string.Empty;

    // All subdirectories derived from Data directory
    [JsonIgnore]
    public string Thumbnails => Path.Combine(Data, "Thumbnails");

    [JsonIgnore]
    public string ExtractedArtwork => Path.Combine(Data, "Extracted Artwork");

    [JsonIgnore]
    public string Plugins => Path.Combine(Data, "Plugins");

    [JsonIgnore]
    public string Models => Path.Combine(Data, "Models");

    [JsonIgnore]
    public string HlsDirectory => Path.Combine(Data, "HLS");

    public static string GetDefaultDataDirectory()
    {
        // Docker: use /data, Normal: use LocalApplicationData/Coral
        return File.Exists("/.dockerenv")
            ? "/data"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coral");
    }
}
