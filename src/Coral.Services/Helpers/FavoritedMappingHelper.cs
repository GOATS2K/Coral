using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Microsoft.EntityFrameworkCore;

namespace Coral.Services.Helpers;

public interface IFavoritedMappingHelper
{
    Task MapFavoritedToTracks(IEnumerable<SimpleTrackDto> tracks);
    Task MapFavoritedToTracks(params SimpleTrackDto[] tracks);
    Task MapFavoritedToTracks(IEnumerable<TrackDto> tracks);
    Task MapFavoritedToTracks(params TrackDto[] tracks);
}

public class FavoritedMappingHelper : IFavoritedMappingHelper
{
    private readonly CoralDbContext _context;

    public FavoritedMappingHelper(CoralDbContext context)
    {
        _context = context;
    }

    public async Task MapFavoritedToTracks(IEnumerable<SimpleTrackDto> tracks)
    {
        var likedTrackIds = await GetLikedTrackIds();

        foreach (var track in tracks)
        {
            track.Favorited = likedTrackIds.Contains(track.Id);
        }
    }

    public Task MapFavoritedToTracks(params SimpleTrackDto[] tracks)
    {
        return MapFavoritedToTracks((IEnumerable<SimpleTrackDto>)tracks);
    }

    public async Task MapFavoritedToTracks(IEnumerable<TrackDto> tracks)
    {
        var likedTrackIds = await GetLikedTrackIds();

        foreach (var track in tracks)
        {
            track.Favorited = likedTrackIds.Contains(track.Id);
        }
    }

    public Task MapFavoritedToTracks(params TrackDto[] tracks)
    {
        return MapFavoritedToTracks((IEnumerable<TrackDto>)tracks);
    }

    private async Task<HashSet<Guid>> GetLikedTrackIds()
    {
        var trackIds = await _context.PlaylistTracks
            .Where(pt => pt.Playlist.Type == PlaylistType.LikedSongs)
            .Select(pt => pt.TrackId)
            .ToListAsync();

        return trackIds.ToHashSet();
    }
}
