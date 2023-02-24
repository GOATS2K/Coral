using Coral.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Services.Helpers
{
    public static class AlbumTypeHelper
    {
        public static AlbumType GetAlbumType(int mainArtistCount, int trackCount)
        {
            var maxTracksForSingle = 2;
            var maxTracksForEP = 4;
            var maxTracksForMiniAlbum = 9;

            if (mainArtistCount > 4)
            {
                return AlbumType.Compilation;
            }

            if (trackCount <= maxTracksForSingle)
            {
                return AlbumType.Single;
            }

            if (trackCount <= maxTracksForEP)
            {
                return AlbumType.EP;
            }

            if (trackCount <= maxTracksForMiniAlbum)
            {
                return AlbumType.MiniAlbum;
            }

            return AlbumType.Album;
        }
    }
}
