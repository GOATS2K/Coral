using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles
{
    public class ArtworkProfile : Profile
    {
        public ArtworkProfile()
        {
            CreateMap<List<Artwork>, ArtworkDto>()
                .ForMember(des => des.Original,
                    opt => opt.MapFrom(src => BuildArtworkUrl(src.FirstOrDefault(a => a.Size == ArtworkSize.Original))))
                .ForMember(des => des.Medium,
                    opt => opt.MapFrom(src => BuildArtworkUrl(src.FirstOrDefault(a => a.Size == ArtworkSize.Medium))))
                .ForMember(des => des.Small,
                    opt => opt.MapFrom(src => BuildArtworkUrl(src.FirstOrDefault(a => a.Size == ArtworkSize.Small))))
                .ForMember(des => des.Colors, opt => opt.MapFrom(src => GetColors(src.FirstOrDefault())));
        }

        private static string[] GetColors(Artwork? artwork)
        {
            return artwork == null ? [] : artwork.Colors;
        }

        public static string BuildArtworkUrl(Artwork? artwork)
        {
            if (artwork is null)
            {
                return "";
            }

            return $"/api/artwork/{artwork.Id}";
        }
    }
}
