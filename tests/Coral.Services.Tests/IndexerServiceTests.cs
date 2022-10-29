using Coral.Database;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Coral.Services.Tests;

public class IndexerServiceTests
{
    private readonly IIndexerService _indexerService;
    private readonly CoralDbContext _testDatabase;

    public IndexerServiceTests()
    {
        var testDatabase = new TestDatabase();
        _indexerService = new IndexerService(testDatabase.Context);
        _testDatabase = testDatabase.Context;
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
        var jupiter = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Jupiter");
        var neptune = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Neptune");

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
        var jupiterArtist = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Jupiter");
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
        var marsArtist = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Mars");
        var unknownArtist = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Unknown Artist");

        Assert.NotNull(marsArtist);
        Assert.NotNull(unknownArtist);
    }
}