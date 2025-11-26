using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Microsoft.EntityFrameworkCore;

namespace Coral.Services;

public interface IPlaylistService
{
    Task<PlaylistDto> GetLikedSongsPlaylist();
    Task<PlaylistDto?> GetPlaylist(Guid playlistId);
    Task AddTrackToLikedSongs(Guid trackId);
    Task RemoveTrackFromLikedSongs(Guid trackId);
    Task ReorderTrack(Guid playlistTrackId, int newPosition);
    Task<HashSet<Guid>> GetLikedTrackIds();
}

public class PlaylistService : IPlaylistService
{
    private readonly CoralDbContext _context;
    private readonly IMapper _mapper;

    public PlaylistService(CoralDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    private async Task<Playlist> GetOrCreateLikedSongsPlaylist()
    {
        var playlist = await _context.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.LikedSongs);

        if (playlist == null)
        {
            playlist = new Playlist
            {
                Name = "Liked Songs",
                Description = "Your favorite tracks",
                Type = PlaylistType.LikedSongs,
                Tracks = new List<PlaylistTrack>()
            };
            _context.Playlists.Add(playlist);
            await _context.SaveChangesAsync();
        }

        return playlist;
    }

    public async Task<PlaylistDto> GetLikedSongsPlaylist()
    {
        var playlist = await GetOrCreateLikedSongsPlaylist();
        var dto = await _context.Playlists
            .Where(p => p.Id == playlist.Id)
            .ProjectTo<PlaylistDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (dto == null)
        {
            return new PlaylistDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = playlist.Description ?? "",
                Tracks = new List<PlaylistTrackDto>(),
                CreatedAt = playlist.CreatedAt,
                UpdatedAt = playlist.UpdatedAt
            };
        }

        foreach (var track in dto.Tracks)
        {
            track.Track.Favorited = true;
        }

        return dto;
    }

    public async Task<PlaylistDto?> GetPlaylist(Guid playlistId)
    {
        return await _context.Playlists
            .Where(p => p.Id == playlistId)
            .ProjectTo<PlaylistDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task AddTrackToLikedSongs(Guid trackId)
    {
        var playlist = await GetOrCreateLikedSongsPlaylist();

        if (await _context.PlaylistTracks.AnyAsync(pt => pt.PlaylistId == playlist.Id && pt.TrackId == trackId))
            return;

        var maxPosition = await _context.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlist.Id)
            .MaxAsync(pt => (int?)pt.Position) ?? -1;

        _context.PlaylistTracks.Add(new PlaylistTrack
        {
            PlaylistId = playlist.Id,
            TrackId = trackId,
            Position = maxPosition + 1
        });
        await _context.SaveChangesAsync();
    }

    public async Task RemoveTrackFromLikedSongs(Guid trackId)
    {
        var playlist = await _context.Playlists
            .FirstOrDefaultAsync(p => p.Type == PlaylistType.LikedSongs);

        if (playlist == null)
            return;

        var playlistTrack = await _context.PlaylistTracks
            .FirstOrDefaultAsync(pt => pt.PlaylistId == playlist.Id && pt.TrackId == trackId);

        if (playlistTrack == null)
            return;

        var removedPosition = playlistTrack.Position;
        _context.PlaylistTracks.Remove(playlistTrack);

        await _context.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlist.Id && pt.Position > removedPosition)
            .ExecuteUpdateAsync(setters => setters.SetProperty(pt => pt.Position, pt => pt.Position - 1));

        await _context.SaveChangesAsync();
    }

    public async Task ReorderTrack(Guid playlistTrackId, int newPosition)
    {
        var playlistTrack = await _context.PlaylistTracks
            .FirstOrDefaultAsync(pt => pt.Id == playlistTrackId);

        if (playlistTrack == null)
            return;

        var oldPosition = playlistTrack.Position;
        if (oldPosition == newPosition)
            return;

        var playlistId = playlistTrack.PlaylistId;

        if (newPosition < oldPosition)
        {
            await _context.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId && pt.Position >= newPosition && pt.Position < oldPosition)
                .ExecuteUpdateAsync(setters => setters.SetProperty(pt => pt.Position, pt => pt.Position + 1));
        }
        else
        {
            await _context.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId && pt.Position > oldPosition && pt.Position <= newPosition)
                .ExecuteUpdateAsync(setters => setters.SetProperty(pt => pt.Position, pt => pt.Position - 1));
        }

        playlistTrack.Position = newPosition;
        await _context.SaveChangesAsync();
    }

    public async Task<HashSet<Guid>> GetLikedTrackIds()
    {
        var trackIds = await _context.PlaylistTracks
            .Where(pt => pt.Playlist.Type == PlaylistType.LikedSongs)
            .Select(pt => pt.TrackId)
            .ToListAsync();

        return trackIds.ToHashSet();
    }
}
