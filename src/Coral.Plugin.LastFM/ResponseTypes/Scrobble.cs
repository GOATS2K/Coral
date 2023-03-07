using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Coral.Plugin.LastFM.ResponseTypes
{
    public class Album
    {
        [JsonPropertyName("corrected")]
        public string Corrected { get; set; } = default!;
    }

    public class AlbumArtist
    {
        [JsonPropertyName("corrected")]
        public string Corrected { get; set; } = default!;

        [JsonPropertyName("#text")]
        public string Text { get; set; } = default!;
    }

    public class Artist
    {
        [JsonPropertyName("corrected")]
        public string Corrected { get; set; } = default!;

        [JsonPropertyName("#text")]
        public string Text { get; set; }  = default!;
    }

    public class Attr
    {
        [JsonPropertyName("ignored")]
        public int Ignored { get; set; }

        [JsonPropertyName("accepted")]
        public int Accepted { get; set; }
    }

    public class IgnoredMessage
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = default!;

        [JsonPropertyName("#text")]
        public string Text { get; set; } = default!;
    }

    public class ScrobbleResponse
    {
        [JsonPropertyName("scrobbles")]
        public Scrobbles Scrobbles { get; set; } = default!;
    }

    public class Scrobble
    {
        [JsonPropertyName("artist")]
        public Artist Artist { get; set; } = default!;

        [JsonPropertyName("album")]
        public Album Album { get; set; } = default!;

        [JsonPropertyName("track")]
        public Track Track { get; set; } = default!;

        [JsonPropertyName("ignoredMessage")]
        public IgnoredMessage IgnoredMessage { get; set; } = default!;

        [JsonPropertyName("albumArtist")]
        public AlbumArtist AlbumArtist { get; set; } = default!;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = default!;
    }

    public class Scrobbles
    {
        [JsonPropertyName("scrobble")]
        public Scrobble Scrobble { get; set; } = default!;

        [JsonPropertyName("@attr")]
        public Attr Attr { get; set; } = default!;
    }

    public class Track
    {
        [JsonPropertyName("corrected")]
        public string Corrected { get; set; } = default!;

        [JsonPropertyName("#text")]
        public string Text { get; set; } = default!;
    }
}
