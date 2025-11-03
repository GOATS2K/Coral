using Coral.Configuration;
using Coral.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Coral.TestProviders;

public class DatabaseFixture : IAsyncLifetime
{
    private SqliteConnection? _connection;
    public TestDatabase TestDb { get; private set; } = null!;

    private void CleanUpTempLibraries()
    {
        var libraries = TestDb.Context.MusicLibraries
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
        var indexedArtwork = TestDb.Context.Artworks
            .Where(a => a.Path.StartsWith(ApplicationConfiguration.Thumbnails)
                        || a.Path.StartsWith(ApplicationConfiguration.ExtractedArtwork))
            .Select(a => a.Path);

        foreach (var artworkPath in indexedArtwork)
        {
            try
            {
                var directory = new DirectoryInfo(artworkPath).Parent;
                File.Delete(artworkPath);
                if (!directory!.GetFiles().Any())
                {
                    directory.Delete();
                }
            }
            catch (Exception) { }
        }
    }

    public async Task InitializeAsync()
    {
        // Create in-memory SQLite connection
        // IMPORTANT: Must keep connection open for in-memory database to persist
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        TestDb = new TestDatabase(opt =>
        {
            opt.UseSqlite(_connection);
        });

        // No Testcontainers, no Docker - instant startup!
    }

    public Task DisposeAsync()
    {
        CleanUpArtwork();
        CleanUpTempLibraries();
        TestDb?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        return Task.CompletedTask;
    }
}