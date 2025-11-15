using AutoMapper;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Microsoft.EntityFrameworkCore;

namespace Coral.Services.Helpers;

public interface IArtworkMappingHelper
{
    Task MapArtworksToAlbums<T>(IEnumerable<T> albums) where T : SimpleAlbumDto;
    Task MapArtworksToAlbums<T>(params T[] albums) where T : SimpleAlbumDto;
}

public class ArtworkMappingHelper : IArtworkMappingHelper
{
    private readonly CoralDbContext _context;
    private readonly IMapper _mapper;

    public ArtworkMappingHelper(CoralDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task MapArtworksToAlbums<T>(IEnumerable<T> albums) where T : SimpleAlbumDto
    {
        var albumList = albums.ToList();
        if (albumList.Count == 0) return;

        var albumIds = albumList.Select(a => a.Id).ToList();
        var artworks = await _context.Artworks
            .Where(a => albumIds.Contains(a.AlbumId))
            .ToListAsync();

        var artworkDict = artworks.ToDictionary(a => a.AlbumId);

        foreach (var album in albumList)
        {
            if (artworkDict.TryGetValue(album.Id, out var artwork))
            {
                album.Artworks = _mapper.Map<ArtworkDto>(artwork);
            }
        }
    }

    public Task MapArtworksToAlbums<T>(params T[] albums) where T : SimpleAlbumDto
    {
        return MapArtworksToAlbums((IEnumerable<T>)albums);
    }
}
