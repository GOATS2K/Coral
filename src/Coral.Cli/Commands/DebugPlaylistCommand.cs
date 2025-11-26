using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Configuration;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Coral.Cli.Commands;

internal class DebugPlaylistCommand : AsyncCommand
{
    private readonly CoralDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAnsiConsole _console;

    public DebugPlaylistCommand(
        CoralDbContext dbContext,
        IMapper mapper,
        IAnsiConsole console)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        _console.MarkupLine("[bold yellow]Debug Playlist Migration[/]");
        _console.MarkupLine($"[dim]Database: {ApplicationConfiguration.SqliteDbPath}[/]");
        _console.WriteLine();

        // 1. Check if LikedSongs playlist exists
        _console.MarkupLine("[blue]1. Checking for LikedSongs playlist...[/]");
        var playlist = await _dbContext.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.LikedSongs);

        if (playlist == null)
        {
            _console.MarkupLine("[red]No LikedSongs playlist found![/]");
            return -1;
        }

        _console.MarkupLine($"[green]Found playlist:[/] {playlist.Id}");
        _console.MarkupLine($"  Name: {playlist.Name}");
        _console.MarkupLine($"  Description: {playlist.Description}");
        _console.MarkupLine($"  Type: {playlist.Type}");
        _console.WriteLine();

        // 2. Count PlaylistTracks via EF
        _console.MarkupLine("[blue]2. Counting PlaylistTracks via EF...[/]");

        // First, check total count without filter
        var totalCount = await _dbContext.PlaylistTracks.CountAsync();
        _console.MarkupLine($"[green]Total PlaylistTracks (no filter):[/] {totalCount}");

        // Debug: Show the SQL that EF generates
        var query = _dbContext.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlist.Id);
        _console.MarkupLine($"[dim]EF Query: {query.ToQueryString()}[/]");

        var trackCount = await query.CountAsync();
        _console.MarkupLine($"[green]PlaylistTracks count (filtered):[/] {trackCount}");

        // 2b. Count PlaylistTracks via raw SQL
        _console.MarkupLine("[blue]2b. Counting PlaylistTracks via raw SQL...[/]");
        var rawCount = await _dbContext.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM PlaylistTracks WHERE PlaylistId = '{playlist.Id}'")
            .FirstOrDefaultAsync();
        _console.MarkupLine($"[green]PlaylistTracks count (SQL):[/] {rawCount}");
        _console.WriteLine();

        if (trackCount == 0 && rawCount == 0)
        {
            _console.MarkupLine("[yellow]No tracks in playlist - nothing to debug.[/]");
            return 0;
        }

        if (trackCount == 0 && rawCount > 0)
        {
            _console.MarkupLine("[red]MISMATCH: EF returns 0 but SQL returns tracks![/]");
            _console.MarkupLine("[yellow]This suggests a schema mismatch or EF mapping issue.[/]");
        }

        // 3. Load first PlaylistTrack with navigation properties
        _console.MarkupLine("[blue]3. Loading first PlaylistTrack with Track navigation...[/]");
        var firstPlaylistTrack = await _dbContext.PlaylistTracks
            .Include(pt => pt.Track)
            .Where(pt => pt.PlaylistId == playlist.Id)
            .FirstOrDefaultAsync();

        if (firstPlaylistTrack == null)
        {
            _console.MarkupLine("[red]Failed to load PlaylistTrack![/]");
            return -1;
        }

        _console.MarkupLine($"[green]PlaylistTrack loaded:[/]");
        _console.MarkupLine($"  Id: {firstPlaylistTrack.Id}");
        _console.MarkupLine($"  TrackId: {firstPlaylistTrack.TrackId}");
        _console.MarkupLine($"  Position: {firstPlaylistTrack.Position}");
        _console.MarkupLine($"  Track loaded: {firstPlaylistTrack.Track != null}");
        if (firstPlaylistTrack.Track != null)
        {
            _console.MarkupLine($"  Track.Title: {firstPlaylistTrack.Track.Title}");
        }
        _console.WriteLine();

        // 4. Try ProjectTo on just PlaylistTracks
        _console.MarkupLine("[blue]4. Testing ProjectTo on PlaylistTracks...[/]");
        try
        {
            var playlistTrackDtos = await _dbContext.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlist.Id)
                .OrderBy(pt => pt.Position)
                .Take(3)
                .ProjectTo<PlaylistTrackDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            _console.MarkupLine($"[green]ProjectTo<PlaylistTrackDto> returned {playlistTrackDtos.Count} items[/]");
            foreach (var dto in playlistTrackDtos)
            {
                _console.MarkupLine($"  - Position {dto.Position}: Track={dto.Track?.Title ?? "NULL"}");
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]ProjectTo<PlaylistTrackDto> failed:[/] {ex.Message}");
            _console.WriteException(ex);
        }
        _console.WriteLine();

        // 5. Try ProjectTo on Playlist
        _console.MarkupLine("[blue]5. Testing ProjectTo on Playlist...[/]");
        try
        {
            var playlistDto = await _dbContext.Playlists
                .Where(p => p.Id == playlist.Id)
                .ProjectTo<PlaylistDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (playlistDto == null)
            {
                _console.MarkupLine("[red]ProjectTo<PlaylistDto> returned null![/]");
            }
            else
            {
                _console.MarkupLine($"[green]ProjectTo<PlaylistDto> succeeded[/]");
                _console.MarkupLine($"  Id: {playlistDto.Id}");
                _console.MarkupLine($"  Name: {playlistDto.Name}");
                _console.MarkupLine($"  Tracks count: {playlistDto.Tracks?.Count ?? -1}");
                if (playlistDto.Tracks != null && playlistDto.Tracks.Count > 0)
                {
                    _console.MarkupLine($"  First track: {playlistDto.Tracks[0].Track?.Title ?? "NULL"}");
                }
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]ProjectTo<PlaylistDto> failed:[/] {ex.Message}");
            _console.WriteException(ex);
        }
        _console.WriteLine();

        // 6. Verify Track exists in Tracks table
        _console.MarkupLine("[blue]6. Verifying Track exists in database...[/]");
        var trackExists = await _dbContext.Tracks
            .AnyAsync(t => t.Id == firstPlaylistTrack.TrackId);
        _console.MarkupLine($"[green]Track {firstPlaylistTrack.TrackId} exists:[/] {trackExists}");

        if (trackExists)
        {
            var track = await _dbContext.Tracks
                .Include(t => t.Album)
                .Include(t => t.Artists)
                    .ThenInclude(awr => awr.Artist)
                .FirstOrDefaultAsync(t => t.Id == firstPlaylistTrack.TrackId);

            if (track != null)
            {
                _console.MarkupLine($"  Title: {track.Title}");
                _console.MarkupLine($"  Album: {track.Album?.Name ?? "NULL"}");
                _console.MarkupLine($"  Artists: {track.Artists?.Count ?? 0}");
            }
        }

        _console.WriteLine();
        _console.MarkupLine("[bold green]Debug complete.[/]");
        return 0;
    }
}
