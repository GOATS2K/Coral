using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
