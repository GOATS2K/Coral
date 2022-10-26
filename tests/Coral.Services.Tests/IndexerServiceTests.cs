using Xunit;

namespace Coral.Services.Tests;

public class IndexerServiceTests
{
    private readonly IIndexerService _indexerService;
    private static readonly string TestDataPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Content");

    public IndexerServiceTests()
    {
        var testDatabase = new TestDatabase();
        _indexerService = new IndexerService(testDatabase.Context);
    }

    [Fact]
    public void EnsureTestFilesExist()
    {
        var contentDir = new DirectoryInfo(TestDataPath);
        var testAlbums = contentDir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
        Assert.True(contentDir.Exists);
        Assert.NotEmpty(testAlbums);
    }

    [Fact]
    public async Task ReadDirectory_MixedAlbums_CreatesTwoAlbums()
    {
        // arrange
        var mixedContentDir = "Mixed Album Tags";
        var contentPath = Path.Join(TestDataPath, mixedContentDir);

        // act
        _indexerService.ReadDirectory(contentPath);

        // assert
        var jupiter = await _indexerService.ListArtists("Jupiter").SingleAsync();
        var neptune = await _indexerService.ListArtists("Neptune").SingleAsync();
        
        Assert.NotEmpty(jupiter.Albums);
        Assert.NotEmpty(neptune.Albums);
        
        Assert.Equal(1, jupiter.Albums.Count());
        Assert.Equal(1, neptune.Albums.Count());
    }

    [Fact]
    public async Task ReadDirectory_JupiterMoons_CreatesValidMetadata()
    {
        // arrange
        var jupiterAlbum = "Jupiter - Moons - 2022 - FLAC [no disc tags]";
        var moonsPath = Path.Join(TestDataPath, jupiterAlbum);
        
        // act
        _indexerService.ReadDirectory(moonsPath);
        
        // assert
        var jupiterArtist = await _indexerService.ListArtists("Jupiter").FirstOrDefaultAsync();
        var moonsAlbum = jupiterArtist?.Albums.FirstOrDefault();
        
        Assert.NotNull(jupiterArtist);
        Assert.NotNull(moonsAlbum);
        // for some reason, this is null when all tests are run together
        Assert.NotNull(moonsAlbum.CoverFilePath);
        Assert.Equal(3, moonsAlbum.Tracks.Count);

        var metisTrack = moonsAlbum.Tracks.Single(t => t.Title == "Metis");
        Assert.Equal(1, metisTrack.TrackNumber);
        Assert.Equal("Noise", metisTrack.Genre?.Name);
        Assert.Equal("this has a comment", metisTrack.Comment);
    }
}