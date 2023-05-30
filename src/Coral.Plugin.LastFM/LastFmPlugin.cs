using Coral.Configuration;
using Coral.PluginBase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.Plugin.LastFM
{
    public class LastFMPlugin : IPlugin
    {
        public string Name => "Last.fm";

        public string Description => "A simple track scrobbler.";

        public IConfiguration AddConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder
                .SetBasePath(ApplicationConfiguration.Plugins)
                .AddJsonFile("LastFmConfiguration.json");
            return configurationBuilder.Build();
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            var configuration = AddConfiguration();
            serviceCollection.Configure<LastFmConfiguration>(configuration);

            serviceCollection.AddScoped<ILastFmService, LastFmService>();
            serviceCollection.AddScoped<IPluginService, LastFmService>();
        }
    }
}
