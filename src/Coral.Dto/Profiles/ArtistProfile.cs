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

        CreateMap<Artist, ArtistDto>()
            .ForMember(des => des.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(des => des.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(des => des.FeaturedIn, opt => opt.MapFrom(src => src.Roles.Where(r => r.Role == ArtistRole.Guest).Select(a => a.Albums).SelectMany(a => a)))
            .ForMember(des => des.RemixerIn, opt => opt.MapFrom(src => src.Roles.Where(r => r.Role == ArtistRole.Remixer).Select(a => a.Albums).SelectMany(a => a)))
            .ForMember(des => des.Releases, opt => opt.MapFrom(src => src.Roles.Where(r => r.Role == ArtistRole.Main).Select(a => a.Albums).SelectMany(a => a)));
    }
}