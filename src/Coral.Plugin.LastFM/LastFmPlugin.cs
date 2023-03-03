using Coral.PluginBase;

namespace Coral.Plugin.LastFM
{
    public class LastFMPlugin : IPlugin
    {
        public string Name => "Last.fm";

        public string Description => "A simple track scrobbler.";
    }
}