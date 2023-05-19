using Coral.Dto.Models;
using System.Diagnostics.CodeAnalysis;

namespace Coral.Dto.Comparers
{
    public class SimpleArtistDtoComparer : IEqualityComparer<SimpleArtistDto>
    {
        public bool Equals(SimpleArtistDto? x, SimpleArtistDto? y)
        {
            if (x?.Id == y?.Id && x?.Name == y?.Name) return true;
            return false;
        }

        public int GetHashCode([DisallowNull] SimpleArtistDto obj)
        {
            return $"{obj.Id}{obj.Name}".GetHashCode();
        }
    }
}
