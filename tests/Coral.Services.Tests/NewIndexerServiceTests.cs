using Coral.Database.Models;
using Coral.Services.Indexer;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Coral.Services.Tests;

internal class NewIndexerServices
{
    public TestDatabase TestDatabase { get; set; } = null!;
    public INewIndexerService IndexerService { get; set; } = null!;
    public IDirectoryScanner DirectoryScanner { get; set; } = null!;
}

public class NewIndexerServiceTests(ITestOutputHelper testOutputHelper)
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

    private NewIndexerServices CreateServices()
    {
        var testDatabase = CreateDatabase();
        var paginationService = new PaginationService(testDatabase.Mapper, testDatabase.Context);
        var searchService = new SearchService(testDatabase.Mapper, testDatabase.Context,
            Substitute.For<ILogger<SearchService>>(), paginationService);
        var artworkService = new ArtworkService(testDatabase.Context,
            Substitute.For<ILogger<ArtworkService>>());
        var indexerService = new NewIndexerService(testDatabase.Context, searchService, artworkService,
            Substitute.For<ILogger<NewIndexerService>>());
        var directoryScanner = new DirectoryScanner(testDatabase.Context,
            Substitute.For<ILogger<DirectoryScanner>>());

        return new NewIndexerServices()
        {
            TestDatabase = testDatabase,
            IndexerService = indexerService,
            DirectoryScanner = directoryScanner
        };
    }

    private async Task ScanLibrary(NewIndexerServices services, MusicLibrary library, bool incremental = false)
    {
        var directoryGroups = services.DirectoryScanner.ScanLibrary(library, incremental);
        var tracks = services.IndexerService.IndexDirectoryGroups(directoryGroups, library);

        await foreach (var track in tracks)
        {
            // Just consume the stream
        }

        await services.IndexerService.FinalizeIndexing(library);
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

        var indexedTrack = services.TestDatabase.Context.Tracks.Include(t => t.AudioFile)
            .FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        Assert.NotNull(indexedTrack);

        var track = new ATL.Track(indexedTrack.AudioFile.FilePath);
        track.Title = "Modified";
        await track.SaveAsync();

        await ScanLibrary(services, library);

        var updatedTrack =
            await services.TestDatabase.Context.Tracks.FirstOrDefaultAsync(t =>
                t.Title == "Modified" && t.Album.Name == indexedTrack.Album.Name);
        Assert.NotNull(updatedTrack);
        Assert.Equal(indexedTrack.Id, updatedTrack.Id);
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
