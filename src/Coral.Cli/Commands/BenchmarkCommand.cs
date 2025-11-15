using System.Diagnostics;
using Coral.Database;
using Coral.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class BenchmarkCommand : AsyncCommand<BenchmarkCommand.Settings>
{
    private readonly ILibraryService _libraryService;
    private readonly ISearchService _searchService;
    private readonly CoralDbContext _dbContext;
    private readonly IAnsiConsole _console;

    public BenchmarkCommand(
        ILibraryService libraryService,
        ISearchService searchService,
        CoralDbContext dbContext,
        IAnsiConsole console)
    {
        _libraryService = libraryService;
        _searchService = searchService;
        _dbContext = dbContext;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[searchQuery]")]
        public string SearchQuery { get; set; } = "Calibre";

        [CommandOption("-n|--number")]
        public int NumberOfAlbums { get; set; } = 10;

        [CommandOption("-i|--iterations")]
        public int Iterations { get; set; } = 3;

        [CommandOption("-w|--warmup")]
        public bool IncludeWarmup { get; set; } = true;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold yellow]Album Fetching Benchmark[/]");
        _console.WriteLine();

        // Prime the database with a search
        await _console.Status()
            .Spinner(Spinner.Known.Arc)
            .StartAsync($"Searching for albums with query: '{settings.SearchQuery}'...", async ctx =>
            {
                var searchResults = await _searchService.Search(settings.SearchQuery, 0, 50);
                _console.MarkupLine($"[green]Found {searchResults.Data.Albums.Count} albums[/]");
            });

        // Get album IDs from the database
        var albumIds = await _dbContext.Albums
            .Take(settings.NumberOfAlbums)
            .Select(a => a.Id)
            .ToListAsync();

        if (albumIds.Count == 0)
        {
            _console.MarkupLine("[red]No albums found in database![/]");
            return -1;
        }

        _console.MarkupLine($"[blue]Testing with {albumIds.Count} albums[/]");
        _console.WriteLine();

        // Warmup run if requested
        if (settings.IncludeWarmup)
        {
            _console.MarkupLine("[dim]Running warmup...[/]");
            foreach (var albumId in albumIds)
            {
                _ = await _libraryService.GetAlbum(albumId);
            }
            _console.MarkupLine("[dim]Warmup complete[/]");
            _console.WriteLine();
        }

        // Create a table for results
        var table = new Table();
        table.AddColumn("Iteration");
        table.AddColumn("Albums Fetched");
        table.AddColumn("Total Time (ms)");
        table.AddColumn("Avg Time per Album (ms)");
        table.AddColumn("Albums/sec");

        var allTimes = new List<double>();

        // Run benchmark iterations
        for (int iteration = 1; iteration <= settings.Iterations; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();

            foreach (var albumId in albumIds)
            {
                _ = await _libraryService.GetAlbum(albumId);
            }

            stopwatch.Stop();

            var totalMs = stopwatch.Elapsed.TotalMilliseconds;
            var avgMs = totalMs / albumIds.Count;
            var albumsPerSec = albumIds.Count / stopwatch.Elapsed.TotalSeconds;

            allTimes.Add(totalMs);

            table.AddRow(
                iteration.ToString(),
                albumIds.Count.ToString(),
                $"{totalMs:F2}",
                $"{avgMs:F2}",
                $"{albumsPerSec:F2}"
            );
        }

        _console.Write(table);
        _console.WriteLine();

        // Summary statistics
        if (settings.Iterations > 1)
        {
            var avgTotalTime = allTimes.Average();
            var minTime = allTimes.Min();
            var maxTime = allTimes.Max();
            var stdDev = Math.Sqrt(allTimes.Sum(x => Math.Pow(x - avgTotalTime, 2)) / allTimes.Count);

            var summaryTable = new Table();
            summaryTable.AddColumn("Metric");
            summaryTable.AddColumn("Value");

            summaryTable.AddRow("Average Total Time", $"{avgTotalTime:F2} ms");
            summaryTable.AddRow("Min Total Time", $"{minTime:F2} ms");
            summaryTable.AddRow("Max Total Time", $"{maxTime:F2} ms");
            summaryTable.AddRow("Std Deviation", $"{stdDev:F2} ms");
            summaryTable.AddRow("Avg Time per Album", $"{avgTotalTime / albumIds.Count:F2} ms");
            summaryTable.AddRow("Avg Albums/sec", $"{albumIds.Count / (avgTotalTime / 1000):F2}");

            _console.MarkupLine("[bold]Summary Statistics:[/]");
            _console.Write(summaryTable);
        }

        return 0;
    }
}