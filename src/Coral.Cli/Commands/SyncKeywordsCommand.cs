using Coral.BulkExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class SyncKeywordsCommand : AsyncCommand<SyncKeywordsCommand.Settings>
{
    private readonly CoralDbContext _dbContext;
    private readonly ISearchService _searchService;
    private readonly IAnsiConsole _console;

    public SyncKeywordsCommand(
        CoralDbContext dbContext,
        ISearchService searchService,
        IAnsiConsole console)
    {
        _dbContext = dbContext;
        _searchService = searchService;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        // No options needed for this simple command
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.WriteLine("Getting tracks...");
        try
        {
            // Query tracks without keywords
            var tracksWithoutKeywords = await _dbContext.Tracks
                .Include(t => t.Keywords)
                .Include(t => t.Artists)
                    .ThenInclude(a => a.Artist)
                .Include(t => t.Album)
                    .ThenInclude(a => a.Label)
                .Where(t => !t.Keywords.Any())
                .ToListAsync();

            if (!tracksWithoutKeywords.Any())
            {
                _console.MarkupLine("[green]All tracks have keywords. Nothing to sync.[/]");
                return 0;
            }

            _console.MarkupLine($"[yellow]Found {tracksWithoutKeywords.Count} tracks without keywords[/]");
            _console.MarkupLine("[blue]Syncing keywords...[/]");

            // Build keyword strings for all tracks
            var trackKeywordStrings = new Dictionary<Track, string>();
            foreach (var track in tracksWithoutKeywords)
            {
                var keywordString = BuildKeywordString(track);
                trackKeywordStrings[track] = keywordString;
            }

            // Insert keywords using bulk method (SearchService handles bulk context)
            await _searchService.InsertKeywordsForTracksBulk(trackKeywordStrings);

            // Save all changes
            var stats = await _dbContext.SaveBulkChangesAsync(
                new BulkInsertOptions(),
                retainCache: false);

            _console.MarkupLine($"[green]Keywords synced successfully for {tracksWithoutKeywords.Count} tracks[/]");
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error syncing keywords: {ex.Message}[/]");
            return 1;
        }
    }

    private static string BuildKeywordString(Track track)
    {
        // Build keyword string in the same format as IndexerService
        var artists = track.Artists ?? new List<ArtistWithRole>();
        var artistString = string.Join(", ", artists.Select(a => a.Artist.Name));
        var releaseYear = track.Album?.ReleaseYear != null ? $"({track.Album.ReleaseYear})" : "";
        var label = track.Album?.Label != null ? $"({track.Album.Label.Name} - {track.Album.CatalogNumber})" : "";

        return $"{artistString} - {track.Title} - {track.Album?.Name ?? ""} {releaseYear} {label}".Trim();
    }
}