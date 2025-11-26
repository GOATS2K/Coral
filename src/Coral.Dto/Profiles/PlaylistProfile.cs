using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class PlaylistProfile : Profile
{
    public PlaylistProfile()
    {
        CreateMap<Playlist, PlaylistDto>()
            .ForMember(dest => dest.Tracks, opt => opt.MapFrom(src => src.Tracks.OrderBy(t => t.Position)));

        CreateMap<PlaylistTrack, PlaylistTrackDto>()
            .ForMember(dest => dest.AddedAt, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.Track, opt => opt.MapFrom(src => src.Track));
    }
}
