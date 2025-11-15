using AutoMapper;
using Coral.Database.Models;
using Coral.Dto.Models;

namespace Coral.Dto.Profiles
{
    public class ArtworkProfile : Profile
    {
        public ArtworkProfile()
        {
            // Map from JSON list structure to DTO using helper method
            // ConvertUsing runs in-memory after query execution, avoiding SQL translation issues
            CreateMap<Artwork, ArtworkDto>()
                .ConvertUsing(src => new ArtworkDto
                {
                    Small = BuildArtworkUrl(src.Id, ArtworkSize.Small, src.GetPath(ArtworkSize.Small)),
                    Medium = BuildArtworkUrl(src.Id, ArtworkSize.Medium, src.GetPath(ArtworkSize.Medium)),
                    Original = BuildArtworkUrl(src.Id, ArtworkSize.Original, src.GetPath(ArtworkSize.Original)),
                    Colors = src.Colors
                });
        }

        private static string BuildArtworkUrl(Guid artworkId, ArtworkSize size, string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            return $"/api/artwork/{artworkId}?size={size}";
        }
    }
}
