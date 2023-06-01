using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles
{
    public class MusicLibraryProfile : Profile
    {
        public MusicLibraryProfile()
        {
            CreateMap<MusicLibrary, MusicLibraryDto>();
        }
    }
}
