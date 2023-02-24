using Coral.Database.Models;
using Coral.Dto.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
