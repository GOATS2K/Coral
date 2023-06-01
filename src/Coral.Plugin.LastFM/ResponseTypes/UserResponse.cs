using Newtonsoft.Json;

namespace Coral.Plugin.LastFM.ResponseTypes
{
    public class Image
    {
        public string Size { get; set; } = default!;

        [JsonProperty("#text")]
        public string Text { get; set; } = default!;
    }

    public class Registered
    {
        public string Unixtime { get; set; } = default!;

        [JsonProperty("#text")]
        public int? Text { get; set; } = default!;
    }

    public class UserResponse
    {
        public User User { get; set; } = default!;
    }

    public class User
    {
        public string Name { get; set; } = default!;
        public string Age { get; set; } = default!;
        public string Subscriber { get; set; } = default!;
        public string Realname { get; set; } = default!;
        public string Bootstrap { get; set; } = default!;
        public string Playcount { get; set; } = default!;
        public string ArtistCount { get; set; } = default!;
        public string Playlists { get; set; } = default!;
        public string TrackCount { get; set; } = default!;
        public string AlbumCount { get; set; } = default!;
        public List<Image> Image { get; set; } = default!;
        public Registered Registered { get; set; } = default!;
        public string Country { get; set; } = default!;
        public string Gender { get; set; } = default!;
        public string Url { get; set; } = default!;
        public string Type { get; set; } = default!;
    }
}
