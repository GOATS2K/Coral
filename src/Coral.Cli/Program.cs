using System.Diagnostics;
using Coral.Configuration;
using Coral.Database;
using Coral.Services;
using Coral.Services.ChannelWrappers;
using Coral.Services.Indexer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Coral Indexer Benchmark ===");
Console.WriteLine($"Database: {ApplicationConfiguration.DatabaseConnectionString}");
Console.WriteLine();

if (args.Length == 0)
{
    Console.WriteLine("Usage: Coral.Cli <library-path>");
    Console.WriteLine("Example: Coral.Cli \"C:\\Benchmark Library\"");
    return;
}

var libraryPath = args[0];

if (!Directory.Exists(libraryPath))
{
    Console.WriteLine($"ERROR: Directory does not exist: {libraryPath}");
    return;
}

Console.WriteLine($"Library path: {libraryPath}");
Console.WriteLine();

// Set up DI container
var services = new ServiceCollection();

// Logging - minimal for benchmark
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Warning); // Only show warnings/errors during benchmark
});

// Database
services.AddDbContext<CoralDbContext>(options =>
{
    options.UseNpgsql(ApplicationConfiguration.DatabaseConnectionString, opt => opt.UseVector());
});

// Services
services.AddAutoMapper(opt =>
{
    opt.AddMaps("Coral.Dto");
});

// Event emitters
services.AddSingleton<Coral.Events.MusicLibraryRegisteredEventEmitter>();

// Channels
services.AddSingleton<Coral.Services.ChannelWrappers.IEmbeddingChannel, Coral.Services.ChannelWrappers.EmbeddingChannel>();

// Indexer services
services.AddScoped<IIndexerService, IndexerService>();
services.AddScoped<Coral.Services.Indexer.INewIndexerService, NewIndexerService>();
services.AddScoped<IDirectoryScanner, DirectoryScanner>();
services.AddScoped<ISearchService, SearchService>();
services.AddScoped<IArtworkService, ArtworkService>();
services.AddScoped<IPaginationService, PaginationService>();

var serviceProvider = services.BuildServiceProvider();

// Ensure database exists and is migrated
Console.WriteLine("Ensuring database is migrated...");
using (var scope = serviceProvider.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
    await context.Database.MigrateAsync();
    Console.WriteLine("Database ready.");
}

Console.WriteLine();
Console.WriteLine("=== Starting Benchmark ===");
Console.WriteLine();

// Create library
using (var scope = serviceProvider.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

    // Remove existing library if it exists
    var existing = await context.MusicLibraries.FirstOrDefaultAsync(l => l.LibraryPath == libraryPath);
    if (existing != null)
    {
        Console.WriteLine("Removing existing library...");
        context.MusicLibraries.Remove(existing);
        await context.SaveChangesAsync();
    }

    // Add new library
    var library = new Coral.Database.Models.MusicLibrary
    {
        LibraryPath = libraryPath,
        AudioFiles = new List<Coral.Database.Models.AudioFile>()
    };

    context.MusicLibraries.Add(library);
    await context.SaveChangesAsync();

    Console.WriteLine($"Library registered: {library.Id}");
    Console.WriteLine();

    // Start benchmark
    var stopwatch = Stopwatch.StartNew();
    Console.WriteLine("Starting scan...");

    var scanner = scope.ServiceProvider.GetRequiredService<IDirectoryScanner>();
    var indexer = scope.ServiceProvider.GetRequiredService<Coral.Services.Indexer.INewIndexerService>();

    var expectedTracks = scanner.CountFiles(library, incremental: false);
    Console.WriteLine($"Expected tracks: {expectedTracks}");
    Console.WriteLine();

    int tracksIndexed = 0;

    var directoryGroups = scanner.ScanLibrary(library, incremental: false);
    var tracks = indexer.IndexDirectoryGroups(directoryGroups, library, CancellationToken.None);

    await foreach (var track in tracks)
    {
        tracksIndexed++;

        if (tracksIndexed % 500 == 0)
        {
            var elapsed = stopwatch.Elapsed.TotalSeconds;
            var tracksPerSec = tracksIndexed / elapsed;
            Console.WriteLine($"Progress: {tracksIndexed}/{expectedTracks} tracks ({tracksPerSec:F2} tracks/sec, {elapsed:F1}s elapsed)");
        }
    }

    await indexer.FinalizeIndexing(library, CancellationToken.None);

    stopwatch.Stop();

    // Print results
    Console.WriteLine();
    Console.WriteLine("=== BENCHMARK RESULTS ===");
    Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"Tracks indexed: {tracksIndexed}");
    Console.WriteLine($"Tracks per second: {tracksIndexed / stopwatch.Elapsed.TotalSeconds:F2}");
    Console.WriteLine($"Average time per track: {stopwatch.Elapsed.TotalMilliseconds / tracksIndexed:F2} ms");
    Console.WriteLine("========================");
}

Console.WriteLine();
Console.WriteLine("Benchmark complete!");
