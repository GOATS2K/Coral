using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class TrackProfile : Profile
{
    public TrackProfile()
    {
        CreateMap<Track, TrackDto>()
            .ForMember(dest => dest.Favorited, opt => opt.MapFrom(src => src.Favorite != null));
        CreateMap<Track, SimpleTrackDto>()
            .ForMember(dest => dest.Favorited, opt => opt.MapFrom(src => src.Favorite != null));
    }
}