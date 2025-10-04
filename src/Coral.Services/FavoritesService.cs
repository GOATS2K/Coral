using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
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
    Task<List<SimpleTrackDto>> GetAllTracks();
    Task<List<SimpleArtistDto>> GetAllArtists();
}

public class FavoritesService : IFavoritesService
{
    private readonly CoralDbContext _context;
    private readonly IMapper _mapper;

    public FavoritesService(CoralDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
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
        if (await _context.FavoriteTracks.AnyAsync(a => a.TrackId == trackId))
            return;
        
        await _context.FavoriteTracks.AddAsync(new FavoriteTrack {TrackId = trackId});
        await _context.SaveChangesAsync();
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
        await _context.FavoriteTracks.Where(t => t.TrackId == trackId).ExecuteDeleteAsync();
        await _context.SaveChangesAsync();
    }

    public async Task RemoveArtist(Guid artistId)
    {
        await _context.FavoriteArtists.Where(t => t.ArtistId == artistId).ExecuteDeleteAsync();
        await _context.SaveChangesAsync();
    }

    public async Task<List<SimpleAlbumDto>> GetAllAlbums()
    {
        var albumIds = _context.FavoriteAlbums.Select(t => t.AlbumId).ToList();
        return await _context
            .Albums
            .Where(c => albumIds.Contains(c.Id))
            .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<List<SimpleTrackDto>> GetAllTracks()
    {
        var trackIds =  _context.FavoriteTracks.Select(t => t.TrackId).ToList();
        return await _context
            .Tracks
            .Where(c => trackIds.Contains(c.Id))
            .ProjectTo<SimpleTrackDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
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