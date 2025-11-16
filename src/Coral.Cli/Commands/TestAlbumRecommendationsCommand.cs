using System.Diagnostics;
using Coral.Database;
using Coral.Database.Models;
using Coral.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class TestAlbumRecommendationsCommand : AsyncCommand<TestAlbumRecommendationsCommand.Settings>
{
    private readonly ILibraryService _libraryService;
    private readonly CoralDbContext _dbContext;
    private readonly IAnsiConsole _console;

    public TestAlbumRecommendationsCommand(
        ILibraryService libraryService,
        CoralDbContext dbContext,
        IAnsiConsole console)
    {
        _libraryService = libraryService;
        _dbContext = dbContext;
        _console = console;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-a|--album-id")]
        public string? AlbumId { get; set; }

        [CommandOption("-n|--album-name")]
        public string? AlbumName { get; set; }

        [CommandOption("-v|--verbose")]
        public bool Verbose { get; set; } = false;

        [CommandOption("-l|--limit")]
        public int Limit { get; set; } = 10;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold yellow]Album Recommendations Test[/]");
        _console.WriteLine();

        // Find the album
        Guid albumId;
        string albumDisplayName;
        string artistName = "";

        if (!string.IsNullOrEmpty(settings.AlbumId))
        {
            if (!Guid.TryParse(settings.AlbumId, out albumId))
            {
                _console.MarkupLine($"[red]Invalid album ID format: {settings.AlbumId}[/]");
                return 1;
            }

            var album = await _dbContext.Albums
                .Include(a => a.Artists)
                .ThenInclude(aa => aa.Artist)
                .FirstOrDefaultAsync(a => a.Id == albumId);

            if (album == null)
            {
                _console.MarkupLine($"[red]Album not found with ID: {albumId}[/]");
                return 1;
            }

            albumDisplayName = album.Name;
            // Get all main artists and join with comma
            var mainArtists = album.Artists
                .Where(aa => aa.Role == ArtistRole.Main)
                .Select(aa => aa.Artist?.Name ?? "Unknown Artist")
                .ToList();
            artistName = mainArtists.Any() ? string.Join(", ", mainArtists) : "Unknown Artist";
        }
        else if (!string.IsNullOrEmpty(settings.AlbumName))
        {
            var searchQuery = settings.AlbumName.ToLower();
            var albums = await _dbContext.Albums
                .Include(a => a.Artists)
                .ThenInclude(aa => aa.Artist)
                .Where(a => a.Name.ToLower().Contains(searchQuery))
                .Take(10)
                .ToListAsync();

            if (!albums.Any())
            {
                _console.MarkupLine($"[red]No albums found matching: {settings.AlbumName}[/]");
                return 1;
            }

            if (albums.Count == 1)
            {
                var album = albums[0];
                albumId = album.Id;
                albumDisplayName = album.Name;
                var mainArtists = album.Artists
                    .Where(aa => aa.Role == ArtistRole.Main)
                    .Select(aa => aa.Artist?.Name ?? "Unknown Artist")
                    .ToList();
                artistName = mainArtists.Any() ? string.Join(", ", mainArtists) : "Unknown Artist";
            }
            else
            {
                // Multiple matches, let user choose
                _console.MarkupLine("[yellow]Multiple albums found:[/]");
                var prompt = new SelectionPrompt<(Guid Id, string Name, string Artist)>()
                    .Title("Select an album:")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more albums)[/]")
                    .UseConverter(a => $"{a.Artist} - {a.Name}");

                foreach (var album in albums)
                {
                    var mainArtists = album.Artists
                        .Where(aa => aa.Role == ArtistRole.Main)
                        .Select(aa => aa.Artist?.Name ?? "Unknown Artist")
                        .ToList();
                    var artist = mainArtists.Any() ? string.Join(", ", mainArtists) : "Unknown Artist";
                    prompt.AddChoice((album.Id, album.Name, artist));
                }

                var selection = _console.Prompt(prompt);
                albumId = selection.Id;
                albumDisplayName = selection.Name;
                artistName = selection.Artist;
            }
        }
        else
        {
            _console.MarkupLine("[red]Please specify either --album-id or --album-name[/]");
            return 1;
        }

        _console.WriteLine();
        _console.MarkupLine($"[cyan]Finding similar albums for:[/] {artistName} - {albumDisplayName}");
        _console.WriteLine();

        // Get track count for the album
        var trackCount = await _dbContext.Tracks
            .Where(t => t.AlbumId == albumId)
            .CountAsync();
        _console.MarkupLine($"[dim]Album has {trackCount} tracks[/]");

        // Get recommendations
        var stopwatch = Stopwatch.StartNew();

        var recommendations = new List<Dto.Models.AlbumRecommendationDto>();
        await _console.Status()
            .Spinner(Spinner.Known.Arc)
            .StartAsync("Analyzing album similarity...", async ctx =>
            {
                ctx.Status = "Finding similar tracks for each album track...";
                recommendations = await _libraryService.GetRecommendationsForAlbum(albumId);
            });

        stopwatch.Stop();

        if (!recommendations.Any())
        {
            _console.WriteLine();
            _console.MarkupLine("[yellow]No similar albums found![/]");
            _console.MarkupLine("[dim]This could mean no tracks have embeddings yet. Run the 'embeddings' command first.[/]");
            return 0;
        }

        // Display results
        _console.WriteLine();
        _console.MarkupLine($"[green]Found {recommendations.Count} similar albums in {stopwatch.ElapsedMilliseconds}ms[/]");
        _console.WriteLine();

        // Create results table
        var table = new Table();
        table.Title = new TableTitle("[bold cyan]Similar Albums[/]");
        table.AddColumn("#", c => c.Centered());
        table.AddColumn("Artist");
        table.AddColumn("Album");
        table.AddColumn("Year");
        table.AddColumn("Similarity", c => c.Centered());

        if (settings.Verbose)
        {
            table.AddColumn("Label");
            table.AddColumn("Album ID");
        }

        var displayCount = Math.Min(settings.Limit, recommendations.Count);
        for (int i = 0; i < displayCount; i++)
        {
            var rec = recommendations[i];
            // Get the main artist name(s) - show "Various Artists" if more than 4
            string artistNames;
            if (rec.Album.Artists?.Any() == true)
            {
                var artistCount = rec.Album.Artists.Count();
                if (artistCount > 4)
                {
                    artistNames = "Various Artists";
                }
                else
                {
                    artistNames = string.Join(", ", rec.Album.Artists.Select(a => a.Name));
                }
            }
            else
            {
                artistNames = "Unknown Artist";
            }

            // Color code similarity percentage
            var similarityColor = rec.SimilarityPercentage switch
            {
                >= 80 => "green",
                >= 60 => "yellow",
                >= 40 => "cyan",
                _ => "red"
            };

            var row = new List<string>
            {
                (i + 1).ToString(),
                artistNames,
                rec.Album.Name ?? "Unknown Album",
                rec.Album.ReleaseYear > 0 ? rec.Album.ReleaseYear.ToString() : "-",
                $"[{similarityColor}]{rec.SimilarityPercentage}%[/]"
            };

            if (settings.Verbose)
            {
                row.Add(rec.SimilarityLabel);
                row.Add(rec.Album.Id.ToString());
            }

            table.AddRow(row.ToArray());
        }

        _console.Write(table);

        // Show detailed track match information if verbose
        if (settings.Verbose)
        {
            _console.WriteLine();
            _console.MarkupLine("[dim]Note: Albums are ranked by the number of tracks that match and their average similarity distance.[/]");
        }

        return 0;
    }
}