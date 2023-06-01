namespace Coral.Plugin.LastFM.ResponseTypes
{
    public class GetSessionResponse
    {
        public Session Session { get; set; } = null!;
    }

    public class Session
    {
        public string Name { get; set; } = null!;
        public string Key { get; set; } = null!;
        public int Subscriber { get; set; }
    }
}
