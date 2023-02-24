using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Helpers;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<Album, AlbumDto>()
            .ForMember(des => des.Artists, opt => opt.MapFrom(src => src.Artists.Where(a => a.Role == ArtistRole.Main)))
            .ForMember(des => des.Type, opt => opt.MapFrom(src => GetAlbumTypeForAlbum(src)));
        CreateMap<Album, SimpleAlbumDto>()
            .ForMember(des => des.Artists, opt => opt.MapFrom(src => src.Artists.Where(a => a.Role == ArtistRole.Main)))
            .ForMember(des => des.Type, opt => opt.MapFrom(src => GetAlbumTypeForAlbum(src)));
    }

    public static AlbumType GetAlbumTypeForAlbum(Album album)
    {
        var type = album.Type ?? AlbumTypeHelper.GetAlbumType(album.Artists.Where(a => a.Role == ArtistRole.Main).Count(), album.Tracks.Count());
        return type;
    }
}