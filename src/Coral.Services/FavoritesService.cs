using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Coral.Services;

public interface IFavoritesService
{
    Task AddAlbum(Guid albumId);
    Task AddTrack(Guid trackId);
    Task AddArtist(Guid artistId);

    Task RemoveAlbum(Guid albumId);
    Task RemoveTrack(Guid trackId);
    Task RemoveArtist(Guid artistId);

    Task<List<SimpleAlbumDto>> GetAllAlbums();
    Task<PlaylistDto> GetAllTracks();
    Task<List<SimpleArtistDto>> GetAllArtists();
}

public class FavoritesService : IFavoritesService
{
    private readonly CoralDbContext _context;
    private readonly IMapper _mapper;
    private readonly IArtworkMappingHelper _artworkMappingHelper;
    private readonly IPlaylistService _playlistService;

    public FavoritesService(CoralDbContext context, IMapper mapper, IArtworkMappingHelper artworkMappingHelper, IPlaylistService playlistService)
    {
        _context = context;
        _mapper = mapper;
        _artworkMappingHelper = artworkMappingHelper;
        _playlistService = playlistService;
    }

    public async Task AddAlbum(Guid albumId)
    {
        if (await _context.FavoriteAlbums.AnyAsync(a => a.AlbumId == albumId))
            return;
        
        await _context.FavoriteAlbums.AddAsync(new FavoriteAlbum {AlbumId = albumId});
        await _context.SaveChangesAsync();
    }

    public async Task AddTrack(Guid trackId)
    {
        await _playlistService.AddTrackToLikedSongs(trackId);
    }

    public async Task AddArtist(Guid artistId)
    {
        if (await _context.FavoriteArtists.AnyAsync(a => a.ArtistId == artistId))
            return;
        
        await _context.FavoriteArtists.AddAsync(new FavoriteArtist {ArtistId = artistId});
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAlbum(Guid albumId)
    {
        await _context.FavoriteAlbums.Where(t => t.AlbumId == albumId).ExecuteDeleteAsync();
        await _context.SaveChangesAsync();
    }

    public async Task RemoveTrack(Guid trackId)
    {
        await _playlistService.RemoveTrackFromLikedSongs(trackId);
    }

    public async Task RemoveArtist(Guid artistId)
    {
        await _context.FavoriteArtists.Where(t => t.ArtistId == artistId).ExecuteDeleteAsync();
        await _context.SaveChangesAsync();
    }

    public async Task<List<SimpleAlbumDto>> GetAllAlbums()
    {
        var albumIds = _context.FavoriteAlbums.Select(t => t.AlbumId).ToList();
        var albums = await _context
            .Albums
            .Where(c => albumIds.Contains(c.Id))
            .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        await _artworkMappingHelper.MapArtworksToAlbums(albums);
        return albums;
    }

    public async Task<PlaylistDto> GetAllTracks()
    {
        return await _playlistService.GetLikedSongsPlaylist();
    }

    public async Task<List<SimpleArtistDto>> GetAllArtists()
    {
        var artistIds =  _context.FavoriteArtists.Select(t => t.ArtistId).ToList();
        return await  _context
            .Artists
            .Where(c => artistIds.Contains(c.Id))
            .ProjectTo<SimpleArtistDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}