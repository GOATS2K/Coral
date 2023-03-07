using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.PluginBase
{
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }

        public void ConfigureServices(IServiceCollection serviceCollection);
    }
}