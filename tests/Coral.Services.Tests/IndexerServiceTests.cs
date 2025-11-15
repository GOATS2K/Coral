using Coral.Database.Models;
using Coral.Events;
using Coral.Services.ChannelWrappers;
using Coral.Services.Helpers;
using Coral.Services.Indexer;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Coral.Services.Tests;

internal class IndexerServices
{
    public TestDatabase TestDatabase { get; set; } = null!;
    public IIndexerService IndexerService { get; set; } = null!;
    public IDirectoryScanner DirectoryScanner { get; set; } = null!;
}

public class IndexerServiceTests(DatabaseFixture fixture)
    : TransactionTestBase(fixture)
{
    private IndexerServices CreateServices()
    {
        var testDatabase = TestDatabase;
        var paginationService = new PaginationService(testDatabase.Mapper, testDatabase.Context);
        var searchService = new SearchService(testDatabase.Mapper, testDatabase.Context,
            Substitute.For<ILogger<SearchService>>(), paginationService, Substitute.For<IArtworkMappingHelper>());
        var artworkService = new ArtworkService(testDatabase.Context,
            Substitute.For<ILogger<ArtworkService>>());
        var eventEmitter = new MusicLibraryRegisteredEventEmitter();

        var indexerService = new IndexerService(
            testDatabase.Context,
            searchService,
            Substitute.For<ILogger<IndexerService>>(),
            artworkService);

        var directoryScanner = new DirectoryScanner(
            testDatabase.Context,
            Substitute.For<ILogger<DirectoryScanner>>());

        return new IndexerServices()
        {
            TestDatabase = testDatabase,
            IndexerService = indexerService,
            DirectoryScanner = directoryScanner
        };
    }

    private async Task ScanLibrary(IndexerServices services, MusicLibrary library, bool incremental = false)
    {
        // Save library to database if it hasn't been saved yet
        if (library.Id == Guid.Empty)
        {
            library.Id = Guid.NewGuid();
            library.AudioFiles = new List<AudioFile>();
            library.CreatedAt = DateTime.UtcNow;
            library.UpdatedAt = DateTime.UtcNow;
            library.LastScan = DateTime.MinValue;
            TestDatabase.Context.MusicLibraries.Add(library);
            await TestDatabase.Context.SaveChangesAsync();
        }

        var directoryGroups = services.DirectoryScanner.ScanLibrary(library, incremental);
        var tracks = services.IndexerService.IndexDirectoryGroups(directoryGroups, library, CancellationToken.None);

        await foreach (var track in tracks)
        {
            // Just consume the stream
        }

        await services.IndexerService.FinalizeIndexing(library, CancellationToken.None);
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
        var services = CreateServices();

        await ScanLibrary(services, TestDataRepository.MixedAlbumTags);

        var jupiter = await services.TestDatabase.Context.ArtistsWithRoles.Include(a => a.Albums)
            .FirstAsync(a => a.Artist.Name == "Jupiter" && a.Role == ArtistRole.Main);
        var neptune = await services.TestDatabase.Context.ArtistsWithRoles.Include(a => a.Albums)
            .FirstAsync(a => a.Artist.Name == "Neptune" && a.Role == ArtistRole.Main);

        Assert.NotEmpty(jupiter.Albums);
        Assert.NotEmpty(neptune.Albums);

        Assert.Single(jupiter.Albums);
        Assert.Single(neptune.Albums);
    }

    [Fact]
    public async Task ReadDirectory_NeptuneDiscovery_IndexesEmbeddedArtwork()
    {
        var services = CreateServices();

        await ScanLibrary(services, TestDataRepository.NeptuneDiscovery);

        var album = await services.TestDatabase.Context.Albums.Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Name == "Discovery");
        Assert.NotNull(album);
        Assert.NotNull(album.Artwork);

        var originalArtwork = album.Artwork.Paths.FirstOrDefault(p => p.Size == ArtworkSize.Original);
        Assert.Equal(480, originalArtwork?.Height);
        Assert.Equal(480, originalArtwork?.Width);
        Assert.Equal(3, album.Artwork.Paths.Count);
    }

    [Fact]
    public async Task ReadDirectory_JupiterMoons_CreatesValidMetadata()
    {
        var services = CreateServices();

        await ScanLibrary(services, TestDataRepository.JupiterMoons);

        var jupiter = services.TestDatabase.Context.Artists.Where(a => a.Name == "Jupiter").ToList();
        var moonsAlbum = services.TestDatabase.Context.Albums
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
        var services = CreateServices();
        var directoryName =
            new DirectoryInfo(TestDataRepository.GetTestFolder("Mars - Moons - FLAC [missing metadata]")).Name;

        await ScanLibrary(services, TestDataRepository.MarsMissingMetadata);

        var marsArtist = await services.TestDatabase.Context.Artists.FirstOrDefaultAsync(a => a.Name == "Mars");
        var unknownArtist = await services.TestDatabase.Context.Artists.FirstOrDefaultAsync(a => a.Name == "Unknown Artist");

        var albumList = await services.TestDatabase.Context.Albums
            .Include(a => a.Tracks)
            .ThenInclude(a => a.Genre)
            .Include(a => a.Artists)
            .Where(a => a.Name == directoryName).ToListAsync();

        Assert.Single(albumList);
        var album = albumList.Single();
        Assert.Equal(2, album.Tracks.Count());

        Assert.NotNull(albumList);
        Assert.NotNull(marsArtist);
        Assert.NotNull(unknownArtist);
    }

    [Fact]
    public async Task ReadDirectory_VariousArtistRelease_AttachesBothArtistsToAlbum()
    {
        var services = CreateServices();

        await ScanLibrary(services, TestDataRepository.NeptuneSaturnRings);

        var ringsAlbum = services.TestDatabase.Context.Albums.Include(a => a.Tracks).ThenInclude(track => track.Artists)
            .Include(a => a.Artists)
            .ThenInclude(a => a.Artist).First(a => a.Name == "Rings");
        var saturn = ringsAlbum.Artists.First(a => a.Artist.Name == "Saturn");
        var neptune = ringsAlbum.Artists.First(a => a.Artist.Name == "Neptune");

        var track = ringsAlbum.Tracks.First(t => t.Title == "We Got Rings");

        Assert.NotNull(track);
        Assert.Equal(2, track.Artists.Count());

        Assert.NotNull(ringsAlbum);
        Assert.NotNull(saturn);
        Assert.NotNull(neptune);
    }

    [Fact]
    public async Task ReadDirectory_MarsWhoDiscoveredMe_ParsesArtistsCorrectly()
    {
        var services = CreateServices();

        await ScanLibrary(services, TestDataRepository.MarsWhoDiscoveredMe);

        var album = await services.TestDatabase.Context.Albums
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

    [Fact]
    public async Task ScanLibrary_ModifyTagsBeforeRescan_PicksUpMetadataChange()
    {
        var services = CreateServices();
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)));

        var library = new MusicLibrary
        {
            LibraryPath = testFolder.FullName,
            LastScan = DateTime.UtcNow,
            AudioFiles = new List<AudioFile>()
        };
        services.TestDatabase.Context.MusicLibraries.Add(library);
        await services.TestDatabase.Context.SaveChangesAsync();

        await ScanLibrary(services, library);

        var indexedTrack = services.TestDatabase.Context.Tracks
            .Include(t => t.AudioFile)
            .Include(t => t.Album)
            .FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        Assert.NotNull(indexedTrack);

        var originalAlbumName = indexedTrack.Album.Name;
        var originalTrackId = indexedTrack.Id;

        var track = new ATL.Track(indexedTrack.AudioFile.FilePath);
        track.Title = "Modified";
        await track.SaveAsync();

        await ScanLibrary(services, library);

        var updatedTrack =
            await services.TestDatabase.Context.Tracks.FirstOrDefaultAsync(t =>
                t.Title == "Modified" && t.Album.Name == originalAlbumName);
        Assert.NotNull(updatedTrack);
        Assert.Equal(originalTrackId, updatedTrack.Id);
    }

    [Fact]
    public async Task ScanLibrary_ModifyAlbumAndGenreTagsBeforeRescan_PicksUpChanges()
    {
        var services = CreateServices();
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)), recursive: true);

        var library = new MusicLibrary
        {
            LibraryPath = testFolder.FullName,
            LastScan = DateTime.UtcNow,
            AudioFiles = new List<AudioFile>()
        };
        services.TestDatabase.Context.MusicLibraries.Add(library);
        await services.TestDatabase.Context.SaveChangesAsync();

        await ScanLibrary(services, library);

        var indexedTracks = await services.TestDatabase.Context.Tracks
            .Include(t => t.AudioFile)
            .Include(t => t.Album)
            .Include(t => t.Genre)
            .Where(t => t.AudioFile.Library.Id == library.Id)
            .ToListAsync();
        Assert.NotEmpty(indexedTracks);

        var originalAlbumName = indexedTracks.First().Album.Name;

        // Modify ALL tracks in the album - only then should the old album be deleted
        foreach (var track in indexedTracks)
        {
            var atlTrack = new ATL.Track(track.AudioFile.FilePath);
            atlTrack.Album = "Modified Album Name";
            atlTrack.Genre = "Modified Genre";
            await atlTrack.SaveAsync();
        }

        await ScanLibrary(services, library);

        services.TestDatabase.Context.ChangeTracker.Clear();

        var updatedTracks = await services.TestDatabase.Context.Tracks
            .Include(t => t.Album)
            .Include(t => t.Genre)
            .Where(t => t.AudioFile.Library.Id == library.Id)
            .ToListAsync();
        Assert.NotEmpty(updatedTracks);

        // All tracks should now have the modified album and genre
        Assert.All(updatedTracks, track =>
        {
            Assert.Equal("Modified Album Name", track.Album.Name);
            Assert.Equal("Modified Genre", track.Genre?.Name);
        });

        // Old album should be deleted since all tracks moved to new album
        var oldAlbum = await services.TestDatabase.Context.Albums
            .FirstOrDefaultAsync(a => a.Name == originalAlbumName);
        Assert.Null(oldAlbum);
    }

    [Fact]
    public async Task ScanLibrary_ModifyArtistTagsBeforeRescan_PicksUpChanges()
    {
        var services = CreateServices();
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)), recursive: true);

        var library = new MusicLibrary
        {
            LibraryPath = testFolder.FullName,
            LastScan = DateTime.UtcNow,
            AudioFiles = new List<AudioFile>()
        };
        services.TestDatabase.Context.MusicLibraries.Add(library);
        await services.TestDatabase.Context.SaveChangesAsync();

        await ScanLibrary(services, library);

        var indexedTracks = await services.TestDatabase.Context.Tracks
            .Include(t => t.AudioFile)
            .Include(t => t.Artists)
            .ThenInclude(a => a.Artist)
            .Where(t => t.AudioFile.Library.Id == library.Id)
            .ToListAsync();
        Assert.NotEmpty(indexedTracks);

        var originalArtistName = indexedTracks.First().Artists.First().Artist.Name;

        // Modify ALL tracks' artist tags
        foreach (var track in indexedTracks)
        {
            var atlTrack = new ATL.Track(track.AudioFile.FilePath);
            atlTrack.Artist = "Modified Artist Name";
            await atlTrack.SaveAsync();
        }

        await ScanLibrary(services, library);

        services.TestDatabase.Context.ChangeTracker.Clear();

        var updatedTracks = await services.TestDatabase.Context.Tracks
            .Include(t => t.Artists)
            .ThenInclude(a => a.Artist)
            .Where(t => t.AudioFile.Library.Id == library.Id)
            .ToListAsync();
        Assert.NotEmpty(updatedTracks);

        // All tracks should now have the modified artist
        Assert.All(updatedTracks, track =>
        {
            Assert.Single(track.Artists);
            Assert.Equal("Modified Artist Name", track.Artists.First().Artist.Name);
        });

        // Old artist should be deleted since all tracks moved to new artist
        var oldArtist = await services.TestDatabase.Context.Artists
            .FirstOrDefaultAsync(a => a.Name == originalArtistName);
        Assert.Null(oldArtist);
    }

    [Fact]
    public async Task ScanLibrary_RenameBeforeRescan_ChangesFilePathOfTrack()
    {
        var services = CreateServices();
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)));

        var library = new MusicLibrary
        {
            LibraryPath = testFolder.FullName,
            LastScan = DateTime.UtcNow,
            AudioFiles = new List<AudioFile>()
        };
        services.TestDatabase.Context.MusicLibraries.Add(library);
        await services.TestDatabase.Context.SaveChangesAsync();

        await ScanLibrary(services, library);

        var indexedTrack = services.TestDatabase.Context.Tracks.Include(t => t.AudioFile)
            .FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        Assert.NotNull(indexedTrack);

        var destinationFileName = Path.Combine(Directory.GetParent(indexedTrack.AudioFile.FilePath)!.FullName,
            "renamed-file.flac");
        File.Move(indexedTrack.AudioFile.FilePath, destinationFileName);

        await ScanLibrary(services, library);

        var updatedTrack = services.TestDatabase.Context.Tracks.Include(t => t.AudioFile)
            .FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        var oldTrack =
            services.TestDatabase.Context.Tracks.FirstOrDefault(f => f.AudioFile.FilePath == indexedTrack.AudioFile.FilePath);
        var oldFile =
            services.TestDatabase.Context.AudioFiles.FirstOrDefault(f => f.FilePath == indexedTrack.AudioFile.FilePath);
        Assert.Null(oldTrack);
        Assert.Null(oldFile);
        Assert.NotNull(updatedTrack);
        Assert.Equal(destinationFileName, updatedTrack.AudioFile.FilePath);
    }

    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = false)
    {
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        DirectoryInfo[] dirs = dir.GetDirectories();

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
}
