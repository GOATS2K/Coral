using Coral.Dto.Models;
using System.Diagnostics.CodeAnalysis;

namespace Coral.Dto.Comparers
{
    public class SimpleAlbumDtoComparer : IEqualityComparer<SimpleAlbumDto>
    {
        public bool Equals(SimpleAlbumDto? x, SimpleAlbumDto? y)
        {

            if (x?.Id == y?.Id
                && x?.Name == y?.Name
                // lazy
                && x?.Artists.Count() == y?.Artists.Count()
                && x?.ReleaseYear == y?.ReleaseYear) return true;
            return false;
        }

        public int GetHashCode([DisallowNull] SimpleAlbumDto obj)
        {
            return $"{obj.Id}{obj.Name}{obj.Artists.Count()}{obj.ReleaseYear}".GetHashCode();
        }
    }
}
