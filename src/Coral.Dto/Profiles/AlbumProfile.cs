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
            .ForMember(des => des.HasArtwork, opt => opt.MapFrom(src => src.Artworks.Any()));
        CreateMap<Album, SimpleAlbumDto>()
            .ForMember(des => des.Artists, opt => opt.MapFrom(src => src.Artists.Where(a => a.Role == ArtistRole.Main)))
            .ForMember(des => des.HasArtwork, opt => opt.MapFrom(src => src.Artworks.Any()));
    }
}