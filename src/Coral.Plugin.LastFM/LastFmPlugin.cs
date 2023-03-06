using Coral.PluginBase;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.Plugin.LastFM
{
    public class LastFMPlugin : IPlugin
    {
        public string Name => "Last.fm";

        public string Description => "A simple track scrobbler.";

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddScoped<ILastFmService, LastFmService>();
        }
    }
}