using Coral.Database;
using Coral.TestProviders;
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
        _testDatabase = testDatabase.Context;
        _indexerService = new IndexerService(testDatabase.Context);
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
        var jupiter = await _testDatabase.Artists.FirstAsync(a => a.Name == "Jupiter");
        var neptune = await _testDatabase.Artists.FirstAsync(a => a.Name == "Neptune");

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
        var directoryName = new DirectoryInfo(TestDataRepository.MarsMissingMetadata).Name;

        // act
        _indexerService.ReadDirectory(TestDataRepository.MarsMissingMetadata);

        // assert
        var marsArtist = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Mars");
        var unknownArtist = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Unknown Artist");

        // take the Android approach and name the albums with no album tag the same as the folder they're in
        var albumList = await _testDatabase.Albums.Where(a => a.Name == directoryName).ToListAsync();

        // ensure only one album was created for both tracks
        Assert.Equal(1, albumList.Count());
        // ensure the album has both tracks
        var album = albumList.Single();
        Assert.Equal(2, album.Tracks.Count());
        


        Assert.NotNull(albumList);
        Assert.NotNull(marsArtist);
        Assert.NotNull(unknownArtist);
    }

    [Fact]
    public void ReadDirectory_VariousArtistRelease_AttachesBothArtistsToAlbum()
    {
        // arrange

        // act
        _indexerService.ReadDirectory(TestDataRepository.NeptuneSaturnRings);

        // assert
        var ringsAlbum = _testDatabase.Albums.First(a => a.Name == "Rings");
        var saturn = ringsAlbum.Artists.First(a => a.Name == "Saturn");
        var neptune = ringsAlbum.Artists.First(a => a.Name == "Neptune");
        Assert.NotNull(ringsAlbum);
        Assert.NotNull(saturn);
        Assert.NotNull(neptune);
    }
}