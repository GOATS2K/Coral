using Coral.PluginHost;

namespace Coral.Api
{
    public class PluginInitializer : IHostedService
    {
        private readonly IPluginContext _pluginContext;

        public PluginInitializer(IPluginContext pluginContext)
        {
            _pluginContext = pluginContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _pluginContext.LoadAssemblies();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _pluginContext.UnloadAll();
            return Task.CompletedTask;
        }
    }
}
