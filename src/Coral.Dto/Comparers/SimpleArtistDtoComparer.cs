using Coral.Dto.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
