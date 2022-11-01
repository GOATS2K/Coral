namespace Coral.TestProviders
{
    public static class TestDataRepository
    {
        public static readonly string ContentDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Content");

        // mixed album tags
        public static readonly string MixedAlbumTags = GetTestFolder("Mixed Album Tags");

        // no disc numbers
        public static readonly string JupiterMoons = GetTestFolder("Jupiter - Moons - 2022 - FLAC [no disc tags]");

        // properly formatted
        public static readonly string NeptuneDiscovery = GetTestFolder("Neptune - Discovery - 2022 - FLAC");

        // missing most metadata
        public static readonly string MarsMissingMetadata = GetTestFolder("Mars - Moons - FLAC [missing metadata]");

        // multi-artist album
        public static readonly string NeptuneSaturnRings = GetTestFolder("Neptune & Saturn - Rings - 2022 - FLAC");

        public static string GetTestFolder(string folderName)
        {
            return Path.Join(ContentDirectory, folderName);
        }
    }
}
