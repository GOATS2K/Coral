using Coral.Configuration;
using Coral.Database;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Coral.TestProviders;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:0.8.1-pg17-trixie")
        .Build();

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
        await _container.StartAsync();
        TestDb = new TestDatabase(opt =>
        {
            opt.UseNpgsql(_container.GetConnectionString(), p => p.UseVector());
        });
    }

    public async Task DisposeAsync()
    {
        CleanUpArtwork();
        CleanUpTempLibraries();
        await _container.StopAsync();
        TestDb?.Dispose();
    }
}