using Coral.BulkExtensions;
using Coral.Database.Models;
using Coral.TestProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Coral.BulkExtensions.Tests;

/// <summary>
/// Tests for bulk inserting albums with tracks and artists.
/// Simulates the indexer workflow.
/// </summary>
public class BulkInsertAlbumTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<BulkInsertAlbumTests> _logger;

    public BulkInsertAlbumTests(DatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = new TestLogger<BulkInsertAlbumTests>(output);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CanInsertSimpleAlbumWithTracksAndArtists()
    {
        // Arrange - simulate indexing a simple album
        var context = _fixture.TestDb.Context;

        // Act - Using new EF Core-style API
        // Create genre using DbSet extension
        var genre = await context.Genres.GetOrAddBulk(
            keySelector: g => g.Name,
            createFunc: () => new Genre
            {
                Id = Guid.NewGuid(),
                Name = "Electronic",
                CreatedAt = DateTime.UtcNow
            });

        // Create artist
        var artist = await context.Artists.GetOrAddBulk(
            keySelector: a => a.Name,
            createFunc: () => new Artist
            {
                Id = Guid.NewGuid(),
                Name = "Daft Punk",
                CreatedAt = DateTime.UtcNow
            });

        // Create artist with role
        var artistWithRole = await context.ArtistsWithRoles.GetOrAddBulk(
            keySelector: awr => new { awr.ArtistId, awr.Role },
            createFunc: () => new ArtistWithRole
            {
                Id = Guid.NewGuid(),
                ArtistId = artist.Id,
                Role = ArtistRole.Main,
                Artist = artist
            });

        // Create album
        var album = await context.Albums.GetOrAddBulk(
            keySelector: a => new { a.Name, a.ReleaseYear },
            createFunc: () => new Album
            {
                Id = Guid.NewGuid(),
                Name = "Random Access Memories",
                ReleaseYear = 2013,
                TrackTotal = 2,
                DiscTotal = 1,
                CreatedAt = DateTime.UtcNow
            });

        // Register album-artist relationship
        context.AddRelationshipBulk(album, artistWithRole);

        // Create library
        var library = await context.MusicLibraries.GetOrAddBulk(
            keySelector: l => l.LibraryPath,
            createFunc: () => new MusicLibrary
            {
                Id = Guid.NewGuid(),
                LibraryPath = "/test/library"
            });

        // Create audio metadata 1
        var audioMetadata1 = await context.AudioMetadata.GetOrAddBulk(
            keySelector: am => new { am.Codec, am.Bitrate, am.SampleRate },
            createFunc: () => new AudioMetadata
            {
                Id = Guid.NewGuid(),
                Codec = "FLAC",
                Bitrate = 1411,
                SampleRate = 44100,
                Channels = 2
            });

        // Create audio file 1
        var audioFile1 = await context.AudioFiles.GetOrAddBulk(
            keySelector: af => af.FilePath,
            createFunc: () => new AudioFile
            {
                Id = Guid.NewGuid(),
                FilePath = "/test/library/track1.flac",
                FileSizeInBytes = 25_000_000,
                Library = library,
                AudioMetadata = audioMetadata1
            });

        // Create track 1
        var track1 = await context.Tracks.GetOrAddBulk(
            keySelector: t => t.AudioFile!.FilePath,
            createFunc: () => new Track
            {
                Id = Guid.NewGuid(),
                Title = "Get Lucky",
                TrackNumber = 1,
                DiscNumber = 1,
                DurationInSeconds = 248,
                Album = album,
                Genre = genre,
                AudioFile = audioFile1,
                CreatedAt = DateTime.UtcNow
            });

        // Register track-artist relationship
        context.AddRelationshipBulk(track1, artistWithRole);

        // Create audio metadata 2 (reuse metadata1 since same format)
        var audioMetadata2 = audioMetadata1; // Same FLAC metadata

        // Create audio file 2
        var audioFile2 = await context.AudioFiles.GetOrAddBulk(
            keySelector: af => af.FilePath,
            createFunc: () => new AudioFile
            {
                Id = Guid.NewGuid(),
                FilePath = "/test/library/track2.flac",
                FileSizeInBytes = 33_000_000,
                Library = library,
                AudioMetadata = audioMetadata2
            });

        // Create track 2
        var track2 = await context.Tracks.GetOrAddBulk(
            keySelector: t => t.AudioFile!.FilePath,
            createFunc: () => new Track
            {
                Id = Guid.NewGuid(),
                Title = "Instant Crush",
                TrackNumber = 2,
                DiscNumber = 1,
                DurationInSeconds = 337,
                Album = album,
                Genre = genre,
                AudioFile = audioFile2,
                CreatedAt = DateTime.UtcNow
            });

        // Register track-artist relationship
        context.AddRelationshipBulk(track2, artistWithRole);

        // EXPLICIT save required - nothing saved automatically!
        var stats = await context.SaveBulkChangesAsync(new BulkInsertOptions { Logger = _logger });

        // Assert
        Assert.NotNull(stats);
        _output.WriteLine($"Total entities inserted: {stats.TotalEntitiesInserted}");
        _output.WriteLine($"Total relationships inserted: {stats.TotalRelationshipsInserted}");
        _output.WriteLine($"Total time: {stats.TotalTime.TotalSeconds:F2}s");

        // Verify the specific entities we created were inserted
        var electronicGenre = await context.Genres
            .FirstOrDefaultAsync(g => g.Name == "Electronic");
        Assert.NotNull(electronicGenre);

        var daftPunk = await context.Artists
            .FirstOrDefaultAsync(a => a.Name == "Daft Punk");
        Assert.NotNull(daftPunk);

        var ramAlbum = await context.Albums
            .Include(a => a.Artists)
            .ThenInclude(a => a.Artist)
            .FirstOrDefaultAsync(a => a.Name == "Random Access Memories" && a.ReleaseYear == 2013);
        Assert.NotNull(ramAlbum);

        // Verify album-artist relationship
        Assert.Single(ramAlbum.Artists);
        Assert.Equal("Daft Punk", ramAlbum.Artists.First().Artist.Name);

        // Verify tracks were created
        var getLucky = await context.Tracks
            .Include(t => t.Artists)
            .ThenInclude(a => a.Artist)
            .Include(t => t.AudioFile)
            .FirstOrDefaultAsync(t => t.Title == "Get Lucky");
        Assert.NotNull(getLucky);
        Assert.Equal("/test/library/track1.flac", getLucky.AudioFile.FilePath);

        var instantCrush = await context.Tracks
            .FirstOrDefaultAsync(t => t.Title == "Instant Crush");
        Assert.NotNull(instantCrush);

        // Verify track-artist relationships
        Assert.Single(getLucky.Artists);
        Assert.Equal("Daft Punk", getLucky.Artists.First().Artist.Name);
    }

    [Fact]
    public async Task CanRegisterMultipleManyToManyRelationshipsForSameEntity()
    {
        // Arrange - simulate a collaboration track with multiple artists
        var context = _fixture.TestDb.Context;

        // Act - Create a single track with 4 different artists
        var genre = await context.Genres.GetOrAddBulk(
            keySelector: g => g.Name,
            createFunc: () => new Genre
            {
                Id = Guid.NewGuid(),
                Name = "Hip Hop",
                CreatedAt = DateTime.UtcNow
            });

        // Create 4 artists for a collaboration
        var artist1 = await context.Artists.GetOrAddBulk(
            keySelector: a => a.Name,
            createFunc: () => new Artist
            {
                Id = Guid.NewGuid(),
                Name = "Kendrick Lamar",
                CreatedAt = DateTime.UtcNow
            });

        var artist2 = await context.Artists.GetOrAddBulk(
            keySelector: a => a.Name,
            createFunc: () => new Artist
            {
                Id = Guid.NewGuid(),
                Name = "SZA",
                CreatedAt = DateTime.UtcNow
            });

        var artist3 = await context.Artists.GetOrAddBulk(
            keySelector: a => a.Name,
            createFunc: () => new Artist
            {
                Id = Guid.NewGuid(),
                Name = "Travis Scott",
                CreatedAt = DateTime.UtcNow
            });

        var artist4 = await context.Artists.GetOrAddBulk(
            keySelector: a => a.Name,
            createFunc: () => new Artist
            {
                Id = Guid.NewGuid(),
                Name = "Jay Rock",
                CreatedAt = DateTime.UtcNow
            });

        // Create artist with role entities for each artist
        var artistWithRole1 = await context.ArtistsWithRoles.GetOrAddBulk(
            keySelector: awr => new { awr.ArtistId, awr.Role },
            createFunc: () => new ArtistWithRole
            {
                Id = Guid.NewGuid(),
                ArtistId = artist1.Id,
                Role = ArtistRole.Main,
                Artist = artist1
            });

        var artistWithRole2 = await context.ArtistsWithRoles.GetOrAddBulk(
            keySelector: awr => new { awr.ArtistId, awr.Role },
            createFunc: () => new ArtistWithRole
            {
                Id = Guid.NewGuid(),
                ArtistId = artist2.Id,
                Role = ArtistRole.Guest,
                Artist = artist2
            });

        var artistWithRole3 = await context.ArtistsWithRoles.GetOrAddBulk(
            keySelector: awr => new { awr.ArtistId, awr.Role },
            createFunc: () => new ArtistWithRole
            {
                Id = Guid.NewGuid(),
                ArtistId = artist3.Id,
                Role = ArtistRole.Guest,
                Artist = artist3
            });

        var artistWithRole4 = await context.ArtistsWithRoles.GetOrAddBulk(
            keySelector: awr => new { awr.ArtistId, awr.Role },
            createFunc: () => new ArtistWithRole
            {
                Id = Guid.NewGuid(),
                ArtistId = artist4.Id,
                Role = ArtistRole.Guest,
                Artist = artist4
            });

        // Create album
        var album = await context.Albums.GetOrAddBulk(
            keySelector: a => new { a.Name, a.ReleaseYear },
            createFunc: () => new Album
            {
                Id = Guid.NewGuid(),
                Name = "Black Panther: The Album",
                ReleaseYear = 2018,
                TrackTotal = 1,
                DiscTotal = 1,
                CreatedAt = DateTime.UtcNow
            });

        // Register all 4 artists to the album
        context.AddRelationshipBulk(album, artistWithRole1);
        context.AddRelationshipBulk(album, artistWithRole2);
        context.AddRelationshipBulk(album, artistWithRole3);
        context.AddRelationshipBulk(album, artistWithRole4);

        // Create library
        var library = await context.MusicLibraries.GetOrAddBulk(
            keySelector: l => l.LibraryPath,
            createFunc: () => new MusicLibrary
            {
                Id = Guid.NewGuid(),
                LibraryPath = "/test/multi-artist"
            });

        // Create audio metadata
        var audioMetadata = await context.AudioMetadata.GetOrAddBulk(
            keySelector: am => new { am.Codec, am.Bitrate, am.SampleRate },
            createFunc: () => new AudioMetadata
            {
                Id = Guid.NewGuid(),
                Codec = "AAC",
                Bitrate = 256,
                SampleRate = 48000,
                Channels = 2
            });

        // Create audio file
        var audioFile = await context.AudioFiles.GetOrAddBulk(
            keySelector: af => af.FilePath,
            createFunc: () => new AudioFile
            {
                Id = Guid.NewGuid(),
                FilePath = "/test/multi-artist/all-the-stars.m4a",
                FileSizeInBytes = 10_000_000,
                Library = library,
                AudioMetadata = audioMetadata
            });

        // Create track
        var track = await context.Tracks.GetOrAddBulk(
            keySelector: t => t.AudioFile!.FilePath,
            createFunc: () => new Track
            {
                Id = Guid.NewGuid(),
                Title = "All The Stars",
                TrackNumber = 1,
                DiscNumber = 1,
                DurationInSeconds = 232,
                Album = album,
                Genre = genre,
                AudioFile = audioFile,
                CreatedAt = DateTime.UtcNow
            });

        // Register ALL 4 artists to the SAME track - this is the key test!
        context.AddRelationshipBulk(track, artistWithRole1);
        context.AddRelationshipBulk(track, artistWithRole2);
        context.AddRelationshipBulk(track, artistWithRole3);
        context.AddRelationshipBulk(track, artistWithRole4);

        // Also test duplicate registration - should be deduplicated by HashSet
        context.AddRelationshipBulk(track, artistWithRole1); // Duplicate!
        context.AddRelationshipBulk(track, artistWithRole2); // Duplicate!

        // Save all changes
        var stats = await context.SaveBulkChangesAsync(new BulkInsertOptions { Logger = _logger });

        // Assert
        Assert.NotNull(stats);
        _output.WriteLine($"Total entities inserted: {stats.TotalEntitiesInserted}");
        _output.WriteLine($"Total relationships inserted: {stats.TotalRelationshipsInserted}");
        _output.WriteLine($"Entity details: {string.Join(", ", stats.EntitiesInserted.Select(kv => $"{kv.Key}: {kv.Value}"))}");
        _output.WriteLine($"Relationship details: {string.Join(", ", stats.RelationshipsInserted.Select(kv => $"{kv.Key}: {kv.Value}"))}");

        // Verify the track was created
        var allTheStars = await context.Tracks
            .Include(t => t.Artists)
            .ThenInclude(a => a.Artist)
            .Include(t => t.Album)
            .ThenInclude(a => a!.Artists)
            .ThenInclude(a => a.Artist)
            .FirstOrDefaultAsync(t => t.Title == "All The Stars");

        Assert.NotNull(allTheStars);

        // CRITICAL ASSERTION: Verify exactly 4 track-artist relationships (not 6, duplicates should be ignored)
        Assert.Equal(4, allTheStars.Artists.Count);

        // Verify all 4 artists are present
        var artistNames = allTheStars.Artists.Select(a => a.Artist.Name).OrderBy(n => n).ToList();
        Assert.Contains("Kendrick Lamar", artistNames);
        Assert.Contains("SZA", artistNames);
        Assert.Contains("Travis Scott", artistNames);
        Assert.Contains("Jay Rock", artistNames);

        // Verify roles
        var mainArtists = allTheStars.Artists.Where(a => a.Role == ArtistRole.Main).ToList();
        var guestArtists = allTheStars.Artists.Where(a => a.Role == ArtistRole.Guest).ToList();
        Assert.Single(mainArtists);
        Assert.Equal(3, guestArtists.Count);
        Assert.Equal("Kendrick Lamar", mainArtists.First().Artist.Name);

        // Verify album also has all 4 artists
        Assert.Equal(4, allTheStars.Album!.Artists.Count);

        _output.WriteLine($"✓ Successfully registered 4 unique relationships for the same track");
        _output.WriteLine($"✓ Duplicate registrations were correctly deduplicated");
    }
}

/// <summary>
/// Simple test logger that writes to xUnit output.
/// </summary>
internal class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }
}
