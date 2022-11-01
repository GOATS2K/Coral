using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.DatabaseModels;

namespace Coral.Dto.Profiles;

public class ArtistProfile : Profile
{
    public ArtistProfile()
    {
        CreateMap<Artist, ArtistDto>();
    }
}