using System.Diagnostics;
using Coral.Database;
using Coral.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class BenchmarkCommand : AsyncCommand<BenchmarkCommand.Settings>
{
    private readonly ISearchService _searchService;
    private readonly CoralDbContext _dbContext;
    private readonly IAnsiConsole _console;

    private static readonly string[] DefaultQueries =
    [
        "calibre",
        "calibre shelflife",
        "calibre shelflife sense",
        "london grammar if you wait"
    ];

    public BenchmarkCommand(
        ISearchService searchService,
        CoralDbContext dbContext,
        IAnsiConsole console)
    {
        _searchService = searchService;
        _dbContext = dbContext;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--rebuild-fts")]
        public bool RebuildFts { get; set; } = false;

        [CommandOption("--show-results")]
        public bool ShowResults { get; set; } = false;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold yellow]Search Engine Benchmark[/]");
        _console.WriteLine();

        if (settings.RebuildFts)
        {
            _console.MarkupLine("[yellow]Rebuilding FTS tables...[/]");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM TrackSearch;");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM AlbumSearch;");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ArtistSearch;");
            await _dbContext.Database.ExecuteSqlRawAsync("INSERT INTO TrackSearch(id, search_text) SELECT Id, SearchText FROM Tracks;");
            await _dbContext.Database.ExecuteSqlRawAsync("INSERT INTO AlbumSearch(id, search_text) SELECT Id, SearchText FROM Albums;");
            await _dbContext.Database.ExecuteSqlRawAsync("INSERT INTO ArtistSearch(id, search_text) SELECT Id, SearchText FROM Artists;");
            _console.MarkupLine("[green]FTS tables rebuilt.[/]");
            _console.WriteLine();
        }

        // Database stats
        var trackCount = await _dbContext.Tracks.CountAsync();
        var albumCount = await _dbContext.Albums.CountAsync();
        var artistCount = await _dbContext.Artists.CountAsync();

        // FTS5 table stats
        var trackFtsCount = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM TrackSearch")
            .FirstAsync();
        var albumFtsCount = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM AlbumSearch")
            .FirstAsync();
        var artistFtsCount = await _dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM ArtistSearch")
            .FirstAsync();

        _console.MarkupLine($"[dim]Database: {trackCount:N0} tracks, {albumCount:N0} albums, {artistCount:N0} artists[/]");
        _console.MarkupLine($"[dim]FTS5: {trackFtsCount:N0} track entries, {albumFtsCount:N0} album entries, {artistFtsCount:N0} artist entries[/]");
        _console.WriteLine();

        var table = new Table();
        table.AddColumn("Query");
        table.AddColumn("Time (ms)");
        table.AddColumn("Tracks");
        table.AddColumn("Albums");
        table.AddColumn("Artists");

        foreach (var query in DefaultQueries)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = await _searchService.Search(query, 0, 50);
            stopwatch.Stop();
            var time = stopwatch.Elapsed.TotalMilliseconds;

            table.AddRow(
                $"[blue]{query}[/]",
                $"{time:F2}",
                results.Data.Tracks.Count.ToString(),
                results.Data.Albums.Count.ToString(),
                results.Data.Artists.Count.ToString()
            );

            if (settings.ShowResults)
            {
                _console.WriteLine();
                _console.MarkupLine($"[bold]Query: {query}[/]");

                if (results.Data.Artists.Any())
                {
                    _console.MarkupLine("[dim]Artists:[/]");
                    foreach (var artist in results.Data.Artists.Take(5))
                    {
                        _console.MarkupLine($"  - {artist.Name}");
                    }
                }

                if (results.Data.Albums.Any())
                {
                    _console.MarkupLine("[dim]Albums:[/]");
                    foreach (var album in results.Data.Albums.Take(5))
                    {
                        _console.MarkupLine($"  - {album.Name} ({album.ReleaseYear})");
                    }
                }

                if (results.Data.Tracks.Any())
                {
                    _console.MarkupLine("[dim]Tracks:[/]");
                    foreach (var track in results.Data.Tracks.Take(5))
                    {
                        var artists = string.Join(", ", track.Artists.Where(a => a.Role == Coral.Database.Models.ArtistRole.Main).Select(a => a.Name));
                        _console.MarkupLine($"  - {artists} - {track.Title} ({track.Album.Name})");
                    }
                }
                _console.WriteLine();
            }
        }

        _console.Write(table);
        return 0;
    }
}