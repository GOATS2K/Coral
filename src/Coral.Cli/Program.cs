using System.Reflection;
using System.Text;
using Coral.Cli.Commands;
using Coral.Cli.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Extensions.DependencyInjection;

// Set up console encoding
Console.OutputEncoding = Encoding.UTF8;

// Get version info
var appVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion.Split("+")[0] ?? "1.0.0";

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

// Configure services
var serviceCollection = ConfigureServices();

// Run CLI app
await RunCommandLineApp(serviceCollection, args);

ServiceCollection ConfigureServices()
{
    var services = new ServiceCollection();
    services.AddSingleton(configuration);
    services.AddCoralServices(configuration);
    return services;
}

async Task RunCommandLineApp(ServiceCollection services, string[] args)
{
#pragma warning disable CA2000
    var typeRegistrar = new DependencyInjectionRegistrar(services);
#pragma warning restore CA2000
    var app = new CommandApp(typeRegistrar);

    app.Configure(opt =>
    {
        opt.Settings.ApplicationName = "coral";
        opt.Settings.ApplicationVersion = appVersion;
        opt.SetExceptionHandler((exception, _) =>
        {
            AnsiConsole.WriteException(exception);
            AnsiConsole.MarkupLine($"[red]Error:[/] {exception.Message}");
        });

        // Index command - indexes a music library
        opt.AddCommand<IndexCommand>("index")
            .WithDescription("Index a music library directory")
            .WithAlias("i")
            .WithExample("index", "\"C:\\Music\"")
            .WithExample("index", "\"C:\\Music\"", "--drop-database")
            .WithExample("index", "\"C:\\Music\"", "--incremental");

        // Embeddings command - generates embeddings for tracks
        opt.AddCommand<EmbeddingsCommand>("embeddings")
            .WithDescription("Generate embeddings for indexed tracks")
            .WithAlias("e")
            .WithExample("embeddings")
            .WithExample("embeddings", "--concurrency 8")
            .WithExample("embeddings", "--min-duration 30", "--max-duration 600");

        // Benchmark command - benchmarks album fetching
        opt.AddCommand<BenchmarkCommand>("benchmark")
            .WithDescription("Benchmark album fetching performance")
            .WithAlias("b")
            .WithExample("benchmark")
            .WithExample("benchmark", "\"search query\"")
            .WithExample("benchmark", "\"Calibre\"", "--number 20", "--iterations 5");

        // Test album recommendations command
        opt.AddCommand<TestAlbumRecommendationsCommand>("test-album-recommendations")
            .WithDescription("Test album recommendations using track intersection algorithm")
            .WithAlias("tar")
            .WithExample("test-album-recommendations", "--album-name", "\"4LYFE\"")
            .WithExample("test-album-recommendations", "--album-id", "\"00000000-0000-0000-0000-000000000000\"")
            .WithExample("test-album-recommendations", "--album-name", "\"A Little While Longer\"", "--verbose", "--limit 20");

        // Clean orphaned embeddings command
        opt.AddCommand<CleanOrphanedEmbeddingsCommand>("clean-orphaned-embeddings")
            .WithDescription("Remove embeddings for tracks that no longer exist in the database")
            .WithExample("clean-orphaned-embeddings")
            .WithExample("clean-orphaned-embeddings", "--dry-run");

        // Test file watcher command
        opt.AddCommand<TestFileWatcherCommand>("test-filewatcher")
            .WithDescription("Test file watcher debouncing logic with live monitoring")
            .WithAlias("tfw")
            .WithExample("test-filewatcher", "\"C:\\Music\"")
            .WithExample("test-filewatcher", "\"C:\\Music\"", "--debounce-seconds 3");

        // Debug playlist command
        opt.AddCommand<DebugPlaylistCommand>("debug-playlist")
            .WithDescription("Debug playlist migration and AutoMapper projection")
            .WithExample("debug-playlist");

        // Rebuild search text command
        opt.AddCommand<RebuildSearchTextCommand>("rebuild-search-text")
            .WithDescription("Rebuild SearchText columns for all tracks, albums, and artists, then refresh FTS tables")
            .WithAlias("rst");
    });

    await app.RunAsync(args);
}