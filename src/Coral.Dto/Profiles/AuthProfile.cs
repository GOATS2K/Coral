using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Auth;

namespace Coral.Dto.Profiles;

public class AuthProfile : Profile
{
    public AuthProfile()
    {
        CreateMap<User, UserDto>();

        CreateMap<Device, DeviceDto>()
            .ForMember(d => d.HasActiveSession, opt => opt.MapFrom(src => src.TokenId != null))
            .ForMember(d => d.IsCurrent, opt => opt.Ignore()); // Set manually in service
    }
}
