using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Coral.Plugin.LastFM.ResponseTypes
{

    public class Nowplaying
    {
        [JsonPropertyName("artist")]
        public Artist Artist { get; set; } = default!;

        [JsonPropertyName("track")]
        public Track Track { get; set; } = default!;

        [JsonPropertyName("ignoredMessage")]
        public IgnoredMessage IgnoredMessage { get; set; } = default!;

        [JsonPropertyName("albumArtist")]
        public AlbumArtist AlbumArtist { get; set; } = default!;

        [JsonPropertyName("album")]
        public Album Album { get; set; } = default!;
    }

    public class NowPlayingResponse
    {
        [JsonPropertyName("nowplaying")]
        public Nowplaying Nowplaying { get; set; } = default!;
    }
}
