using Xunit;

namespace Coral.Services.Tests;

public class IndexerServiceTests
{
    private readonly IIndexerService _indexerService;
    private readonly ILibraryService _libraryService;

    public IndexerServiceTests()
    {
        var testDatabase = new TestDatabase();
        _indexerService = new IndexerService(testDatabase.Context);
        _libraryService = new LibraryService(testDatabase.Context);
    }

    [Fact]
    public void EnsureTestFilesExist()
    {
        var contentDir = new DirectoryInfo(TestDataRepository.ContentDirectory);
        var testAlbums = contentDir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
        Assert.True(contentDir.Exists);
        Assert.NotEmpty(testAlbums);
    }

    [Fact]
    public async Task ReadDirectory_MixedAlbums_CreatesTwoAlbums()
    {
        // arrange

        // act
        _indexerService.ReadDirectory(TestDataRepository.MixedAlbumTags);

        // assert
        var jupiter = await _libraryService.GetArtist("Jupiter").SingleAsync();
        var neptune = await _libraryService.GetArtist("Neptune").SingleAsync();

        Assert.NotEmpty(jupiter.Albums);
        Assert.NotEmpty(neptune.Albums);

        Assert.Single(jupiter.Albums);
        Assert.Single(neptune.Albums);
    }

    [Fact]
    public async Task ReadDirectory_JupiterMoons_CreatesValidMetadata()
    {
        // arrange

        // act
        _indexerService.ReadDirectory(TestDataRepository.JupiterMoons);

        // assert
        var jupiterArtist = await _libraryService.GetArtist("Jupiter").FirstOrDefaultAsync();
        var moonsAlbum = jupiterArtist?.Albums.FirstOrDefault();

        Assert.NotNull(jupiterArtist);
        Assert.NotNull(moonsAlbum);

        Assert.NotNull(moonsAlbum.CoverFilePath);
        Assert.Equal(3, moonsAlbum.Tracks.Count);

        var metisTrack = moonsAlbum.Tracks.Single(t => t.Title == "Metis");
        Assert.Equal(1, metisTrack.TrackNumber);
        Assert.Equal("Noise", metisTrack.Genre?.Name);
        Assert.Equal("this has a comment", metisTrack.Comment);
    }

    [Fact]
    public async Task ReadDirectory_MissingMetadata_IndexesOK()
    {
        // arrange

        // act
        _indexerService.ReadDirectory(TestDataRepository.MarsMissingMetadata);

        // arrange
        var marsArtist = await _libraryService.GetArtist("Mars").FirstOrDefaultAsync();
        var unknownArtist = await _libraryService.GetArtist("Unknown Artist").FirstOrDefaultAsync();

        Assert.NotNull(marsArtist);
        Assert.NotNull(unknownArtist);
    }
}