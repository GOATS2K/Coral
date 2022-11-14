using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<Album, AlbumDto>()
            .ForMember(des => des.CoverPresent, opt => opt.MapFrom(src => src.CoverFilePath != null));
        CreateMap<Album, SimpleAlbumDto>()
            .ForMember(des => des.CoverPresent, opt => opt.MapFrom(src => src.CoverFilePath != null));
    }
}