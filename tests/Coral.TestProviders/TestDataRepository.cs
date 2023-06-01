using Coral.Database.Models;

namespace Coral.TestProviders
{
    public static class TestDataRepository
    {
        public static readonly string ContentDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Content");

        // mixed album tags
        public static readonly MusicLibrary MixedAlbumTags = new()
        {
            LibraryPath = GetTestFolder("Mixed Album Tags")
        };

        // no disc numbers
        public static readonly MusicLibrary JupiterMoons = new()
        {
            LibraryPath = GetTestFolder("Jupiter - Moons - 2022 - FLAC [no disc tags]")
        };

        // properly formatted
        public static readonly MusicLibrary NeptuneDiscovery = new() { LibraryPath = GetTestFolder("Neptune - Discovery - 2022 - FLAC") };

        // missing most metadata
        public static readonly MusicLibrary MarsMissingMetadata = new() { LibraryPath = GetTestFolder("Mars - Moons - FLAC [missing metadata]") };

        // multi-artist album
        public static readonly MusicLibrary NeptuneSaturnRings = new() { LibraryPath = GetTestFolder("Neptune & Saturn - Rings - 2022 - FLAC") };

        // artist parser test - has both guest artist and remixer
        public static readonly MusicLibrary MarsWhoDiscoveredMe = new() { LibraryPath = GetTestFolder("Mars - Who Discovered Me - 2023 - FLAC") };

        public static string GetTestFolder(string folderName)
        {
            return Path.Join(ContentDirectory, folderName);
        }
    }
}
