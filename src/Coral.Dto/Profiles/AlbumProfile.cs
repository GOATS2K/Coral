using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<Album, AlbumDto>()
            .ForMember(des => des.Artists, opt => opt.MapFrom(src => src.Artists.Where(a => a.Role == ArtistRole.Main)))
            .ForMember(dest => dest.Favorited, opt => opt.MapFrom(src => src.Favorite != null))
            .ForMember(dest => dest.Artworks, opt => opt.Ignore()); // Mapped post-query to avoid JSON translation issues

        CreateMap<Album, SimpleAlbumDto>()
            .ForMember(des => des.Artists, opt => opt.MapFrom(src => src.Artists.Where(a => a.Role == ArtistRole.Main)))
            .ForMember(dest => dest.Favorited, opt => opt.MapFrom(src => src.Favorite != null))
            .ForMember(dest => dest.Artworks, opt => opt.Ignore()); // Mapped post-query to avoid JSON translation issues

        CreateMap<Album, SimpleTrackAlbumDto>();
    }
}