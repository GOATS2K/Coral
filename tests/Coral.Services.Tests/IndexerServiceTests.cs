using Coral.Configuration;
using Coral.Database;
using Coral.Database.Models;
using Coral.Events;
using Coral.Services.ChannelWrappers;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Coral.Services.Tests;

internal class Services
{
    public TestDatabase TestDatabase { get; set; } = null!;
    public IIndexerService IndexerService { get; set; } = null!;

}

// These tests will overlap, so using `ContainerTest` makes sure
// that we create a new Postgres database for each test.
public class IndexerServiceTests(ITestOutputHelper testOutputHelper)
    : ContainerTest<PostgreSqlBuilder, PostgreSqlContainer>(testOutputHelper)
{
    protected override PostgreSqlBuilder Configure(PostgreSqlBuilder builder)
    {
        return new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:0.8.1-pg17-trixie");
    }

    private TestDatabase CreateDatabase()
    {
        return new TestDatabase(opt =>
        {
            opt.UseNpgsql(Container.GetConnectionString(), p => p.UseVector());
        });
    }

    private Services CreateServices()
    {
        var testDatabase = CreateDatabase();
        var searchLogger = Substitute.For<ILogger<SearchService>>();
        var indexerLogger = Substitute.For<ILogger<IndexerService>>();
        var artworkLogger = Substitute.For<ILogger<ArtworkService>>();
        var paginationService = Substitute.For<IPaginationService>();
        var embeddingChannel = new EmbeddingChannel();
        var searchService =
            new SearchService(testDatabase.Mapper, testDatabase.Context, searchLogger, paginationService);
        var artworkService = new ArtworkService(testDatabase.Context, artworkLogger);
        var eventEmitter = new MusicLibraryRegisteredEventEmitter();
        var indexerService = new IndexerService(testDatabase.Context, searchService, indexerLogger, artworkService,
            eventEmitter, testDatabase.Mapper, embeddingChannel);

        return new Services()
        {
            TestDatabase = testDatabase,
            IndexerService = indexerService,
        };
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
        var services = CreateServices();

        // act
        await services.IndexerService.ScanLibrary(TestDataRepository.MixedAlbumTags);

        // assert
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
        // arrange
        var services = CreateServices();

        // act
        await services.IndexerService.ScanLibrary(TestDataRepository.NeptuneDiscovery);

        // assert
        var album = await services.TestDatabase.Context.Albums.Include(a => a.Artworks)
            .FirstOrDefaultAsync(a => a.Name == "Discovery");
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
        var services = CreateServices();

        // act
        await services.IndexerService.ScanLibrary(TestDataRepository.JupiterMoons);

        // assert
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
        // arrange
        var services = CreateServices();
        var directoryName =
            new DirectoryInfo(TestDataRepository.GetTestFolder("Mars - Moons - FLAC [missing metadata]")).Name;

        // act
        await services.IndexerService.ScanLibrary(TestDataRepository.MarsMissingMetadata);

        // assert
        var marsArtist = await services.TestDatabase.Context.Artists.FirstOrDefaultAsync(a => a.Name == "Mars");
        var unknownArtist = await services.TestDatabase.Context.Artists.FirstOrDefaultAsync(a => a.Name == "Unknown Artist");

        // take the Android approach and name the albums with no album tag the same as the folder they're in
        var albumList = await services.TestDatabase.Context.Albums
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
        var services = CreateServices();
        
        // act
        await services.IndexerService.ScanLibrary(TestDataRepository.NeptuneSaturnRings);

        // assert
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
        // arrange
        var services = CreateServices();

        // act
        await services.IndexerService.ScanLibrary(TestDataRepository.MarsWhoDiscoveredMe);

        // assert
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

    // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = false)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    [Fact]
    public async Task ScanLibraries_RescanWithNewFiles_PicksUpNewAudioFiles()
    {
        // arrange
        var services = CreateServices();
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var moons = TestDataRepository.JupiterMoons.LibraryPath;
        var tracksOnMoons = Directory
            .EnumerateFiles(moons, "*.*", SearchOption.TopDirectoryOnly)
            .Count(f => Path.GetExtension(f) == ".flac");
        CopyDirectory(moons, Path.Combine(testFolder.FullName, Path.GetFileName(moons)));
        // register library
        var library = await services.IndexerService.AddMusicLibrary(testFolder.FullName);
        // act 1
        await services.IndexerService.ScanLibraries();

        // assert 1
        Assert.NotNull(library);
        var insertedMoons = services.TestDatabase.Context.Tracks.Count(a => a.Album.Name == "Moons"
                                                                      && a.AudioFile.Library.Id == library.Id);
        Assert.Equal(tracksOnMoons, insertedMoons);

        // ----

        // arrange 2
        var neptune = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(neptune, Path.Combine(testFolder.FullName, Path.GetFileName(neptune)));

        // act 2
        await services.IndexerService.ScanLibraries();

        // assert 2
        var tracksOnDiscovery = Directory
            .EnumerateFiles(neptune, "*.*", SearchOption.TopDirectoryOnly)
            .Count(f => Path.GetExtension(f) == ".flac");
        var insertedDiscovery = services.TestDatabase.Context.Tracks.Count(a => a.Album.Name == "Discovery"
                                                                          && a.AudioFile.Library.Id == library.Id);
        Assert.Equal(tracksOnDiscovery, insertedDiscovery);
    }

    [Fact]
    public async Task ScanLibraries_ModifyTagsBeforeRescan_PicksUpMetadataChange()
    {
        // arrange
        var services = CreateServices();
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)));
        var library = await services.IndexerService.AddMusicLibrary(testFolder.FullName);

        // run scan before change
        await services.IndexerService.ScanLibraries();

        // verify that the release was written
        Assert.NotNull(library);
        var indexedTrack = services.TestDatabase.Context.Tracks.Include(t => t.AudioFile)
            .FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        Assert.NotNull(indexedTrack);

        var track = new ATL.Track(indexedTrack.AudioFile.FilePath);
        track.Title = "Modified";
        await track.SaveAsync();

        // act
        await services.IndexerService.ScanLibraries();

        // assert
        var updatedTrack =
            await services.TestDatabase.Context.Tracks.FirstOrDefaultAsync(t =>
                t.Title == "Modified" && t.Album.Name == indexedTrack.Album.Name);
        Assert.NotNull(updatedTrack);
        // verify that the same track was updated
        Assert.Equal(indexedTrack.Id, updatedTrack.Id);
    }

    [Fact]
    public async Task ScanLibraries_RenameBeforeRescan_ChangesFilePathOfTrack()
    {
        // arrange
        var services = CreateServices();
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)));
        var library = await services.IndexerService.AddMusicLibrary(testFolder.FullName);

        // run scan before change
        await services.IndexerService.ScanLibraries();

        // verify that the release was written
        Assert.NotNull(library);
        var indexedTrack = services.TestDatabase.Context.Tracks.Include(t => t.AudioFile)
            .FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        Assert.NotNull(indexedTrack);

        var destinationFileName = Path.Combine(Directory.GetParent(indexedTrack.AudioFile.FilePath)!.FullName,
            "renamed-file.flac");
        File.Move(indexedTrack.AudioFile.FilePath, destinationFileName);

        // act
        await services.IndexerService.ScanLibraries();

        // assert
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
}