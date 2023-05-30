using Coral.Configuration;
using Coral.Database;
using Coral.Database.Models;
using Coral.Events;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
        var paginationService = Substitute.For<IPaginationService>();
        var searchService = new SearchService(testDatabase.Mapper, testDatabase.Context, searchLogger, paginationService);
        var artworkService = new ArtworkService(testDatabase.Context, artworkLogger, testDatabase.Mapper);
        var eventEmitter = new MusicLibraryRegisteredEventEmitter();

        _testDatabase = testDatabase.Context;
        _indexerService = new IndexerService(testDatabase.Context, searchService, indexerLogger, artworkService, eventEmitter, testDatabase.Mapper);
    }
    public void Dispose()
    {
        CleanUpArtwork();
        CleanUpTempLibraries();
        _testDatabase.Dispose();
    }

    private void CleanUpTempLibraries()
    {
        var libraries = _testDatabase.MusicLibraries
            .Where(l => l.LibraryPath != "");
        foreach (var library in libraries)
        {
            if (!Guid.TryParse(Path.GetFileName(library.LibraryPath), out _)) continue;
            var directory = new DirectoryInfo(library.LibraryPath);
            foreach (var file in directory.EnumerateFiles("*.*", SearchOption.AllDirectories))
            {
                file.Delete();
            }
            foreach (var directoryInLibrary in directory.EnumerateDirectories("*.*", SearchOption.AllDirectories))
            {
                directoryInLibrary.Delete();
            }
            directory.Delete();
        }
    }

    private void CleanUpArtwork()
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
        await _indexerService.ScanLibrary(TestDataRepository.MixedAlbumTags);

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
        await _indexerService.ScanLibrary(TestDataRepository.NeptuneDiscovery);

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
        await _indexerService.ScanLibrary(TestDataRepository.JupiterMoons);

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
        await _indexerService.ScanLibrary(TestDataRepository.MarsMissingMetadata);

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
        await _indexerService.ScanLibrary(TestDataRepository.NeptuneSaturnRings);

        // assert
        var ringsAlbum = _testDatabase.Albums.Include(a => a.Tracks).Include(a => a.Artists).ThenInclude(a => a.Artist).First(a => a.Name == "Rings");
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

        // act
        await _indexerService.ScanLibrary(TestDataRepository.MarsWhoDiscoveredMe);

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
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var moons = TestDataRepository.JupiterMoons.LibraryPath;
        var tracksOnMoons = Directory
            .EnumerateFiles(moons, "*.*", SearchOption.TopDirectoryOnly)
            .Count(f => Path.GetExtension(f) == ".flac");
        CopyDirectory(moons, Path.Combine(testFolder.FullName, Path.GetFileName(moons)));
        // register library
        var library = await _indexerService.AddMusicLibrary(testFolder.FullName);
        // act 1
        await _indexerService.ScanLibraries();

        // assert 1
        Assert.NotNull(library);
        var insertedMoons = _testDatabase.Tracks.Count(a => a.Album.Name == "Moons" 
                                                            && a.AudioFile.Library.Id == library.Id);
        Assert.Equal(tracksOnMoons, insertedMoons);

        // ----
        
        // arrange 2
        var neptune = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(neptune, Path.Combine(testFolder.FullName, Path.GetFileName(neptune)));
        
        // act 2
        await _indexerService.ScanLibraries();
        
        // assert 2
        var tracksOnDiscovery = Directory
            .EnumerateFiles(neptune, "*.*", SearchOption.TopDirectoryOnly)
            .Count(f => Path.GetExtension(f) == ".flac");
        var insertedDiscovery = _testDatabase.Tracks.Count(a => a.Album.Name == "Discovery" 
                                                            && a.AudioFile.Library.Id == library.Id);
        Assert.Equal(tracksOnDiscovery, insertedDiscovery);
    }

    [Fact]
    public async Task ScanLibraries_ModifyTagsBeforeRescan_PicksUpMetadataChange()
    {
        // arrange
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)));
        var library = await _indexerService.AddMusicLibrary(testFolder.FullName);

        // run scan before change
        await _indexerService.ScanLibraries();

        // verify that the release was written
        Assert.NotNull(library);
        var indexedTrack = _testDatabase.Tracks.Include(t => t.AudioFile).FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        Assert.NotNull(indexedTrack);

        var track = new ATL.Track(indexedTrack.AudioFile.FilePath);
        track.Title = "Modified";
        await track.SaveAsync();

        // act
        await _indexerService.ScanLibraries();

        // assert
        var updatedTrack = await _testDatabase.Tracks.FirstOrDefaultAsync(t => t.Title == "Modified" && t.Album.Name == indexedTrack.Album.Name);
        Assert.NotNull(updatedTrack);
        // verify that the same track was updated
        Assert.Equal(indexedTrack.Id, updatedTrack.Id);
    }

    [Fact]
    public async Task ScanLibraries_RenameBeforeRescan_ChangesFilePathOfTrack()
    {
        // arrange
        var testFolder = Directory.CreateDirectory(TestDataRepository.GetTestFolder(Guid.NewGuid().ToString()));
        var discovery = TestDataRepository.NeptuneDiscovery.LibraryPath;
        CopyDirectory(discovery, Path.Combine(testFolder.FullName, Path.GetFileName(discovery)));
        var library = await _indexerService.AddMusicLibrary(testFolder.FullName);

        // run scan before change
        await _indexerService.ScanLibraries();

        // verify that the release was written
        Assert.NotNull(library);
        var indexedTrack = _testDatabase.Tracks.Include(t => t.AudioFile).FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        Assert.NotNull(indexedTrack);

        var destinationFileName = Path.Combine(Directory.GetParent(indexedTrack.AudioFile.FilePath)!.FullName, "renamed-file.flac");
        File.Move(indexedTrack.AudioFile.FilePath, destinationFileName);

        // act
        await _indexerService.ScanLibraries();

        // assert
        var updatedTrack = _testDatabase.Tracks.Include(t => t.AudioFile).FirstOrDefault(t => t.Title == "Gallileo" && t.AudioFile.Library.Id == library.Id);
        var oldTrack = _testDatabase.Tracks.FirstOrDefault(f => f.AudioFile.FilePath == indexedTrack.AudioFile.FilePath);
        var oldFile = _testDatabase.AudioFiles.FirstOrDefault(f => f.FilePath == indexedTrack.AudioFile.FilePath);
        Assert.Null(oldTrack);
        Assert.Null(oldFile);
        Assert.NotNull(updatedTrack);
        Assert.Equal(destinationFileName, updatedTrack.AudioFile.FilePath);
    }
}