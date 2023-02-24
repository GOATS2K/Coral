using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class ArtistProfile : Profile
{
    public ArtistProfile()
    {
        CreateMap<ArtistWithRole, ArtistWithRoleDto>()
            .ForMember(des => des.Id, opt => opt.MapFrom(src => src.ArtistId))
            .ForMember(des => des.Name, opt => opt.MapFrom(src => src.Artist.Name));
            
        CreateMap<ArtistWithRole, SimpleArtistDto>()
            .ForMember(des => des.Id, opt => opt.MapFrom(src => src.ArtistId))
            .ForMember(des => des.Name, opt => opt.MapFrom(src => src.Artist.Name));

        CreateMap<Artist, SimpleArtistDto>();
    }
}