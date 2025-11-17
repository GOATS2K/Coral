using System.Diagnostics;
using Coral.Configuration;
using Coral.Database;
using Coral.Database.Models;
using Coral.Services;
using Coral.Services.Indexer;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class IndexCommand : AsyncCommand<IndexCommand.Settings>
{
    private readonly CoralDbContext _dbContext;
    private readonly IDirectoryScanner _directoryScanner;
    private readonly IIndexerService _indexerService;
    private readonly IAnsiConsole _console;

    public IndexCommand(
        CoralDbContext dbContext,
        IDirectoryScanner directoryScanner,
        IIndexerService indexerService,
        IAnsiConsole console)
    {
        _dbContext = dbContext;
        _directoryScanner = directoryScanner;
        _indexerService = indexerService;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<library-path>")]
        public required string LibraryPath { get; set; }

        [CommandOption("--drop-database")]
        public bool DropDatabase { get; set; }

        [CommandOption("--incremental")]
        public bool Incremental { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold yellow]Coral Music Library Indexer[/]");
        _console.MarkupLine($"[dim]Database: SQLite at {ApplicationConfiguration.SqliteDbPath}[/]");
        _console.WriteLine();

        if (!Directory.Exists(settings.LibraryPath))
        {
            _console.MarkupLine($"[red]ERROR: Directory does not exist: {settings.LibraryPath}[/]");
            return -1;
        }

        _console.MarkupLine($"[blue]Library path:[/] {settings.LibraryPath}");
        _console.WriteLine();

        // Drop and recreate database if requested
        if (settings.DropDatabase)
        {
            await _console.Status()
                .Spinner(Spinner.Known.Arc)
                .StartAsync("Dropping existing database...", async ctx =>
                {
                    await _dbContext.Database.EnsureDeletedAsync();
                    ctx.Status = "Creating and migrating database...";
                    await _dbContext.Database.MigrateAsync();
                });
            _console.MarkupLine("[green]Database ready.[/]");
            _console.WriteLine();
        }
        else
        {
            // Ensure database is migrated
            await _dbContext.Database.MigrateAsync();
        }

        // Create or get library
        var library = await GetOrCreateLibrary(settings.LibraryPath);

        // Start indexing
        var stopwatch = Stopwatch.StartNew();

        var expectedTracks = await _directoryScanner.CountFiles(library, incremental: settings.Incremental);
        _console.MarkupLine($"[blue]Expected tracks:[/] {expectedTracks}");
        _console.WriteLine();

        var tracksIndexed = 0;
        var progressBar = _console.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            });

        await progressBar.StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[yellow]Indexing tracks[/]", maxValue: expectedTracks);

            var directoryGroups = _directoryScanner.ScanLibrary(library, incremental: settings.Incremental);
            var tracks = _indexerService.IndexDirectoryGroups(directoryGroups, library, CancellationToken.None);

            await foreach (var track in tracks)
            {
                tracksIndexed++;
                task.Increment(1);

                if (tracksIndexed % 500 == 0)
                {
                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    var tracksPerSec = tracksIndexed / elapsed;
                    task.Description = $"[yellow]Indexing tracks[/] ({tracksPerSec:F2} tracks/sec)";
                }
            }

            task.Description = "[yellow]Finalizing indexing...[/]";
            await _indexerService.FinalizeIndexing(library, CancellationToken.None);
            task.StopTask();
        });

        stopwatch.Stop();

        // Print results
        _console.WriteLine();
        var resultTable = new Table();
        resultTable.Title = new TableTitle("[bold green]Indexing Complete[/]");
        resultTable.AddColumn("Metric");
        resultTable.AddColumn("Value");

        resultTable.AddRow("Total time", $"{stopwatch.Elapsed.TotalSeconds:F2} seconds");
        resultTable.AddRow("Tracks indexed", tracksIndexed.ToString());
        resultTable.AddRow("Tracks per second", $"{tracksIndexed / stopwatch.Elapsed.TotalSeconds:F2}");
        resultTable.AddRow("Average time per track", $"{stopwatch.Elapsed.TotalMilliseconds / tracksIndexed:F2} ms");

        _console.Write(resultTable);

        return 0;
    }

    private async Task<MusicLibrary> GetOrCreateLibrary(string libraryPath)
    {
        var existing = await _dbContext.MusicLibraries.FirstOrDefaultAsync(l => l.LibraryPath == libraryPath);
        if (existing != null)
        {
            _console.MarkupLine($"[yellow]Using existing library: {existing.Id}[/]");
            return existing;
        }

        var library = new MusicLibrary
        {
            LibraryPath = libraryPath,
            AudioFiles = new List<AudioFile>()
        };

        _dbContext.MusicLibraries.Add(library);
        await _dbContext.SaveChangesAsync();

        _console.MarkupLine($"[green]Library registered: {library.Id}[/]");
        return library;
    }
}