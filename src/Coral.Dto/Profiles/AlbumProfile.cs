using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<Album, AlbumDto>()
            .ForMember(des => des.Artists, opt => opt.MapFrom(src => src.ArtistsWithRoles.DistinctBy(a => a.ArtistId).Where(a => a.Role == ArtistRole.Main).Select(a => a.Artist)));
        CreateMap<Album, SimpleAlbumDto>()
            .ForMember(des => des.Artists, opt => opt.MapFrom(src => src.ArtistsWithRoles.DistinctBy(a => a.ArtistId).Where(a => a.Role == ArtistRole.Main).Select(a => a.Artist)));
    }
}