using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.DatabaseModels;

namespace Coral.Dto.Profiles;

public class TrackProfile : Profile
{
    public TrackProfile()
    {
        CreateMap<Track, TrackDto>();
    }
}