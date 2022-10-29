using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles;

public class GenreProfile : Profile
{
    public GenreProfile()
    {
        CreateMap<Genre, GenreDto>();
    }
}