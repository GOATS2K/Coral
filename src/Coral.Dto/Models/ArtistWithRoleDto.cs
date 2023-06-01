using Coral.Database.Models;

namespace Coral.Dto.Models
{
    public record ArtistWithRoleDto : SimpleArtistDto
    {
        public ArtistRole Role { get; set; }
    }
}
