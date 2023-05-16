using System.Net.Mime;
using Coral.Configuration;
using Coral.Database;
using Coral.Database.Models;
using Coral.Events;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SQLitePCL;
using Xunit;

namespace Coral.Services.Tests;

public class IndexerServiceTests : IDisposable
{
    private readonly IIndexerService _indexerService;
    private readonly CoralDbContext _testDatabase;

    public IndexerServiceTests()
    {
        var testDatabase = new TestDatabase();
        var searchLogger = Substitute.For<ILogger<SearchService>>();
        var indexerLogger = Substitute.For<ILogger<IndexerService>>();
        var artworkLogger = Substitute.For<ILogger<ArtworkService>>();
        var paginationService = new PaginationService(testDatabase.Mapper, testDatabase.Context);
        var searchService = new SearchService(testDatabase.Mapper, testDatabase.Context, searchLogger, paginationService);
        var artworkService = new ArtworkService(testDatabase.Context, artworkLogger, testDatabase.Mapper);
        var eventEmitter = new MusicLibraryRegisteredEventEmitter();

        _testDatabase = testDatabase.Context;
        _indexerService = new IndexerService(testDatabase.Context, searchService, indexerLogger, artworkService, eventEmitter);
    }
    public void Dispose()
    {
        var indexedArtwork = _testDatabase.Artworks
            .Where(a => a.Path.StartsWith(ApplicationConfiguration.Thumbnails)
            || a.Path.StartsWith(ApplicationConfiguration.ExtractedArtwork))
            .Select(a => a.Path);

        foreach (var artworkPath in indexedArtwork)
        {
            var directory = new DirectoryInfo(artworkPath).Parent;
            File.Delete(artworkPath);
            if (!directory!.GetFiles().Any())
            {
                directory.Delete();
            }
        }
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
        await _indexerService.ReadDirectory(TestDataRepository.MixedAlbumTags);

        // assert
        var jupiter = await _testDatabase.ArtistsWithRoles.Include(a => a.Albums).FirstAsync(a => a.Artist.Name == "Jupiter" && a.Role == ArtistRole.Main);
        var neptune = await _testDatabase.ArtistsWithRoles.Include(a => a.Albums).FirstAsync(a => a.Artist.Name == "Neptune" && a.Role == ArtistRole.Main);

        Assert.NotEmpty(jupiter.Albums);
        Assert.NotEmpty(neptune.Albums);

        Assert.Single(jupiter.Albums);
        Assert.Single(neptune.Albums);
    }

    [Fact]
    public async Task ReadDirectory_NeptuneDiscovery_IndexesEmbeddedArtwork()
    {
        // arrange

        // act
        await _indexerService.ReadDirectory(TestDataRepository.NeptuneDiscovery);

        // assert
        var album = await _testDatabase.Albums.Include(a => a.Artworks).FirstOrDefaultAsync(a => a.Name == "Discovery");
        Assert.NotNull(album);

        var originalArtwork = album.Artworks.FirstOrDefault(a => a.Size == ArtworkSize.Original);
        Assert.Equal(480, originalArtwork?.Height);
        Assert.Equal(480, originalArtwork?.Width);
        Assert.Equal(3, album.Artworks.Count);
    }

    [Fact]
    public async Task ReadDirectory_JupiterMoons_CreatesValidMetadata()
    {
        // arrange

        // act
        await _indexerService.ReadDirectory(TestDataRepository.JupiterMoons);

        // assert
        var jupiter = _testDatabase.Artists.Where(a => a.Name == "Jupiter").ToList();
        var moonsAlbum = _testDatabase.Albums
            .Include(a => a.Tracks)
            .ThenInclude(a => a.Genre)
            .Include(a => a.Artists)
            .FirstOrDefault(a => a.Name == "Moons");
        Assert.NotNull(moonsAlbum);

        Assert.Single(jupiter);
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
        var directoryName = new DirectoryInfo(TestDataRepository.GetTestFolder("Mars - Moons - FLAC [missing metadata]")).Name;

        // act
        await _indexerService.ReadDirectory(TestDataRepository.MarsMissingMetadata);

        // assert
        var marsArtist = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Mars");
        var unknownArtist = await _testDatabase.Artists.FirstOrDefaultAsync(a => a.Name == "Unknown Artist");

        // take the Android approach and name the albums with no album tag the same as the folder they're in
        var albumList = await _testDatabase.Albums
            .Include(a => a.Tracks)
            .ThenInclude(a => a.Genre)
            .Include(a => a.Artists)
            .Where(a => a.Name == directoryName).ToListAsync();

        // ensure only one album was created for both tracks
        Assert.Single(albumList);
        // ensure the album has both tracks
        var album = albumList.Single();
        Assert.Equal(2, album.Tracks.Count());

        Assert.NotNull(albumList);
        Assert.NotNull(marsArtist);
        Assert.NotNull(unknownArtist);
    }

    [Fact]
    public async Task ReadDirectory_VariousArtistRelease_AttachesBothArtistsToAlbum()
    {
        // arrange

        // act
        await _indexerService.ReadDirectory(TestDataRepository.NeptuneSaturnRings);

        // assert
        var ringsAlbum = _testDatabase.Albums.Include(a => a.Tracks).Include(a => a.Artists).ThenInclude(a => a.Artist).First(a => a.Name == "Rings");
        var saturn = ringsAlbum.Artists.First(a => a.Artist.Name == "Saturn");
        var neptune = ringsAlbum.Artists.First(a => a.Artist.Name == "Neptune");
        Assert.NotNull(ringsAlbum);
        Assert.NotNull(saturn);
        Assert.NotNull(neptune);
    }

    [Fact]
    public async Task ReadDirectory_NeptuneSaturnRings_CreatesTwoArtistsForLastTrack()
    {
        // arrange

        // act
        await _indexerService.ReadDirectory(TestDataRepository.NeptuneSaturnRings);

        // assert
        var ringsAlbum = _testDatabase.Albums.Include(a => a.Tracks).ThenInclude(t => t.Artists).First(a => a.Name == "Rings");
        var track = ringsAlbum.Tracks.First(t => t.Title == "We Got Rings");

        Assert.NotNull(ringsAlbum);
        Assert.NotNull(track);
        Assert.Equal(2, track.Artists.Count());
    }

    [Fact]
    public async Task ReadDirectory_MarsWhoDiscoveredMe_ParsesArtistsCorrectly()
    {
        // arrange

        // act
        await _indexerService.ReadDirectory(TestDataRepository.MarsWhoDiscoveredMe);

        // assert
        var album = await _testDatabase.Albums
            .Include(a => a.Tracks)
            .ThenInclude(a => a.Artists)
            .ThenInclude(a => a.Artist)
            .Include(a => a.Artists)
            .FirstAsync(a => a.Name == "Who Discovered Me?");
        var track = album.Tracks.First();

        Assert.NotNull(track);
        Assert.Equal(3, track.Artists.Count);

        var mars = track.Artists.First(a => a.Artist.Name == "Mars");
        var copernicus = track.Artists.First(a => a.Artist.Name == "Copernicus");
        var nasa = track.Artists.First(a => a.Artist.Name == "NASA");

        Assert.Equal(ArtistRole.Main, mars.Role);
        Assert.Equal(ArtistRole.Guest, copernicus.Role);
        Assert.Equal(ArtistRole.Remixer, nasa.Role);
    }
}