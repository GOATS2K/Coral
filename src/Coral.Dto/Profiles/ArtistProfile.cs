using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class ArtistProfile : Profile
{
    public ArtistProfile()
    {
        CreateMap<Artist, ArtistDto>();
    }
}