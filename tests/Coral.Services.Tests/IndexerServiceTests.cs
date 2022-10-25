using Xunit;

namespace Coral.Services.Tests;

public class IndexerServiceTests : IClassFixture<TestDatabase>
{
    private TestDatabase _testDatabase;
    private IIndexerService _indexerService;
    private static readonly string TestDataPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Content");

    public IndexerServiceTests(TestDatabase testDatabase)
    {
        _testDatabase = testDatabase;
        _indexerService = new IndexerService(testDatabase.Context);
    }

    [Fact]
    public void EnsureTestFilesExist()
    {
        var testFiles = new DirectoryInfo(TestDataPath);
        Assert.NotEmpty(testFiles.GetFiles());
    }

    [Fact]
    public async Task IndexDirectory_JupiterMoons_CreatesValidMetadata()
    {
        // arrange
        var jupiterAlbum = "Jupiter - Moons - 2022 - FLAC [no disc tags]";
        var moonsPath = Path.Join(TestDataPath, jupiterAlbum);
        
        // act
        _indexerService.IndexDirectory(moonsPath);
        
        // assert
        var jupiterArtist = await _indexerService.GetArtist("Jupiter").FirstOrDefaultAsync();
        var moonsAlbum = jupiterArtist?.Albums.FirstOrDefault();
        
        Assert.NotNull(jupiterArtist);
        Assert.NotNull(moonsAlbum);
        Assert.NotNull(moonsAlbum.CoverFilePath);
        Assert.Equal(3, moonsAlbum.Tracks.Count);
    }
}