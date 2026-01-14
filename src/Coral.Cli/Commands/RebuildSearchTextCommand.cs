using System.Text;
using Coral.Database;
using Coral.Database.Models;
using Diacritics.Extensions;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class RebuildSearchTextCommand : AsyncCommand<RebuildSearchTextCommand.Settings>
{
    private readonly CoralDbContext _dbContext;
    private readonly IAnsiConsole _console;

    public RebuildSearchTextCommand(CoralDbContext dbContext, IAnsiConsole console)
    {
        _dbContext = dbContext;
        _console = console;
    }

    public class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Rebuild track SearchText
            await RebuildTrackSearchText();

            // Rebuild album SearchText
            await RebuildAlbumSearchText();

            // Rebuild artist SearchText
            await RebuildArtistSearchText();

            // Rebuild FTS tables
            await RebuildFtsTables();

            _console.MarkupLine("[green]SearchText rebuild complete![/]");
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error rebuilding SearchText: {ex.Message}[/]");
            return 1;
        }
    }

    private async Task RebuildTrackSearchText()
    {
        var tracks = await _dbContext.Tracks
            .Include(t => t.Artists)
                .ThenInclude(a => a.Artist)
            .Include(t => t.Album)
                .ThenInclude(a => a.Label)
            .Include(t => t.Genre)
            .ToListAsync();

        _console.MarkupLine($"[yellow]Rebuilding SearchText for {tracks.Count} tracks...[/]");

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Processing tracks", maxValue: tracks.Count);

                foreach (var track in tracks)
                {
                    track.SearchText = BuildTrackSearchText(track);
                    task.Increment(1);
                }

                await _dbContext.SaveChangesAsync();
            });

        _console.MarkupLine("[green]Track SearchText rebuilt.[/]");
    }

    private async Task RebuildAlbumSearchText()
    {
        var albums = await _dbContext.Albums
            .Include(a => a.Artists)
                .ThenInclude(ar => ar.Artist)
            .Include(a => a.Label)
            .Include(a => a.Tracks)
                .ThenInclude(t => t.Genre)
            .ToListAsync();

        _console.MarkupLine($"[yellow]Rebuilding SearchText for {albums.Count} albums...[/]");

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Processing albums", maxValue: albums.Count);

                foreach (var album in albums)
                {
                    album.SearchText = BuildAlbumSearchText(album);
                    task.Increment(1);
                }

                await _dbContext.SaveChangesAsync();
            });

        _console.MarkupLine("[green]Album SearchText rebuilt.[/]");
    }

    private async Task RebuildArtistSearchText()
    {
        var artists = await _dbContext.Artists.ToListAsync();

        _console.MarkupLine($"[yellow]Rebuilding SearchText for {artists.Count} artists...[/]");

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Processing artists", maxValue: artists.Count);

                foreach (var artist in artists)
                {
                    var original = artist.Name.ToLowerInvariant();
                    var normalized = original.RemoveDiacritics();
                    artist.SearchText = original == normalized ? original : $"{original} {normalized}";
                    task.Increment(1);
                }

                await _dbContext.SaveChangesAsync();
            });

        _console.MarkupLine("[green]Artist SearchText rebuilt.[/]");
    }

    private async Task RebuildFtsTables()
    {
        _console.MarkupLine("[yellow]Rebuilding FTS tables...[/]");

        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM TrackSearch;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM AlbumSearch;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ArtistSearch;");

        await _dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO TrackSearch(id, search_text) SELECT Id, SearchText FROM Tracks;");
        await _dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO AlbumSearch(id, search_text) SELECT Id, SearchText FROM Albums;");
        await _dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO ArtistSearch(id, search_text) SELECT Id, SearchText FROM Artists;");

        _console.MarkupLine("[green]FTS tables rebuilt.[/]");
    }

    private static string BuildTrackSearchText(Track track)
    {
        var sb = new StringBuilder();
        sb.Append(track.Title);

        foreach (var artist in track.Artists ?? Enumerable.Empty<ArtistWithRole>())
        {
            sb.Append(' ').Append(artist.Artist.Name);
        }

        if (track.Album != null)
        {
            sb.Append(' ').Append(track.Album.Name);
            if (track.Album.ReleaseYear.HasValue)
                sb.Append(' ').Append(track.Album.ReleaseYear.Value);
            if (track.Album.Label != null)
            {
                sb.Append(' ').Append(track.Album.Label.Name);
                if (!string.IsNullOrEmpty(track.Album.CatalogNumber))
                    sb.Append(' ').Append(track.Album.CatalogNumber);
            }
        }

        if (track.Genre != null)
            sb.Append(' ').Append(track.Genre.Name);

        if (!string.IsNullOrEmpty(track.Isrc))
            sb.Append(' ').Append(track.Isrc);

        // Store both original (for exact match) and normalized (for diacritics-insensitive match)
        var original = sb.ToString().ToLowerInvariant();
        var normalized = original.RemoveDiacritics();
        return original == normalized ? original : $"{original} {normalized}";
    }

    private static string BuildAlbumSearchText(Album album)
    {
        var sb = new StringBuilder();
        sb.Append(album.Name);

        foreach (var artist in album.Artists ?? Enumerable.Empty<ArtistWithRole>())
        {
            sb.Append(' ').Append(artist.Artist.Name);
        }

        if (album.ReleaseYear.HasValue)
            sb.Append(' ').Append(album.ReleaseYear.Value);

        // Get genre from first track (albums don't have direct genre)
        var genre = album.Tracks?.FirstOrDefault()?.Genre;
        if (genre != null)
            sb.Append(' ').Append(genre.Name);

        if (album.Label != null)
        {
            sb.Append(' ').Append(album.Label.Name);
            if (!string.IsNullOrEmpty(album.CatalogNumber))
                sb.Append(' ').Append(album.CatalogNumber);
        }

        // Store both original (for exact match) and normalized (for diacritics-insensitive match)
        var original = sb.ToString().ToLowerInvariant();
        var normalized = original.RemoveDiacritics();
        return original == normalized ? original : $"{original} {normalized}";
    }
}
