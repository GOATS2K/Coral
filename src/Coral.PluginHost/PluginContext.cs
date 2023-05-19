using Coral.Configuration;
using Coral.PluginBase;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Coral.PluginHost
{
    public interface IPluginContext
    {
        public void UnloadAll();
        public void LoadAssemblies();
        public TType GetService<TType>()
            where TType : class;
    }

    public class PluginContext : IPluginContext
    {
        private readonly ConcurrentDictionary<LoadedPlugin, ServiceProvider> _loadedPlugins = new();
        private readonly ILogger<PluginContext> _logger;
        private readonly ILogger<PluginLoader> _pluginLoaderLogger;
        private readonly ApplicationPartManager _applicationPartManager;
        private readonly MyActionDescriptorChangeProvider _actionDescriptorChangeProvider;
        private readonly IServiceProvider _serviceProvider;


        public PluginContext(ApplicationPartManager applicationPartManager, ILogger<PluginContext> logger, MyActionDescriptorChangeProvider actionDescriptorChangeProvider, IServiceProvider serviceProvider, ILogger<PluginLoader> pluginLoaderLogger)
        {
            _applicationPartManager = applicationPartManager;
            _logger = logger;
            _actionDescriptorChangeProvider = actionDescriptorChangeProvider;
            _serviceProvider = serviceProvider;
            _pluginLoaderLogger = pluginLoaderLogger;
        }   

        public TType GetService<TType>()
            where TType : class
        {
            var targetPlugin = _loadedPlugins.Keys.FirstOrDefault(k => k.LoadedAssembly.GetExportedTypes().Any(x => x == typeof(TType)));
            ArgumentNullException.ThrowIfNull(targetPlugin);
            var serviceProvider = _loadedPlugins[targetPlugin];
            return serviceProvider.GetRequiredService<TType>();
        }
        
        public void UnloadAll()
        {
            foreach (var (plugin, serviceProvider) in _loadedPlugins)
            {
                UnregisterEventHandlersOnPlugin(serviceProvider);
                UnloadPlugin(plugin);
            }
        }

        private void UnloadPlugin(LoadedPlugin plugin)
        {
            _logger.LogInformation("Unloading plugin: {PluginName}", plugin.Plugin.Name);

            _loadedPlugins.Remove(plugin, out _);
            plugin.PluginLoader.Unload();

            var applicationPartToRemove = _applicationPartManager.ApplicationParts.FirstOrDefault(a => a.Name == plugin.LoadedAssembly.GetName().Name);
            if (applicationPartToRemove != null)
            {
                _applicationPartManager.ApplicationParts.Remove(applicationPartToRemove);
                _logger.LogInformation("Unloading plugin controller.");
                _actionDescriptorChangeProvider.TokenSource.Cancel();
            }
        }

        private IServiceCollection ConfigureServiceCollectionForPlugin(IPlugin plugin)
        {
            // set up servicecollection
            var serviceCollection = new ServiceCollection();
            // run ConfigureServices with new service collection        
            plugin.ConfigureServices(serviceCollection);
            serviceCollection.AddLogging(opt => opt.AddConsole());
            // allow plugins to access host services via proxy

            // it is important to note that the ServiceProxy in the plugin service collection
            // would normally contain a reference to its own service provider

            // so here we are telling the service collection to create the proxy
            // using the service provider injected in this class
            serviceCollection.AddScoped<IHostServiceProxy, HostServiceProxy>(_ => new HostServiceProxy(_serviceProvider));
            return serviceCollection;
        }

        private void RegisterEventHandlersOnPlugin(IServiceProvider serviceProvider)
        {
            var service = serviceProvider.GetRequiredService<IPluginService>();
            service.RegisterEventHandlers();
        }

        private void UnregisterEventHandlersOnPlugin(IServiceProvider serviceProvider)
        {
            var service = serviceProvider.GetRequiredService<IPluginService>();
            service.UnregisterEventHandlers();
        }


        public void LoadAssemblies()
        {
            // load plugin via PluginLoader
            var assemblyDirectories = Directory.GetDirectories(ApplicationConfiguration.Plugins);
            foreach (var assemblyDirectoryToLoad in assemblyDirectories)
            {
                var pluginLoader = new PluginLoader(_pluginLoaderLogger);
                var loadedPlugin = pluginLoader.LoadPluginAssemblies(assemblyDirectoryToLoad);
                if (!loadedPlugin.HasValue)
                {
                    continue;
                }

                var storedPlugin = new LoadedPlugin()
                {
                    LoadedAssembly = loadedPlugin.Value.Assembly,
                    Plugin = loadedPlugin.Value.Plugin,
                    PluginLoader = pluginLoader
                };

                var serviceCollection = ConfigureServiceCollectionForPlugin(storedPlugin.Plugin);

                // get controller from plugin
                // note that if a plugin has multiple controllers, this will allow them all to load
                // even if only one of them is a subclass of PluginBaseController
                var controller = loadedPlugin.Value.Assembly.GetTypes().SingleOrDefault(t => t.IsSubclassOf(typeof(PluginBaseController)));
                if (controller == null)
                {
                    return;
                }
                // load controller assembly
                // build service provider for assembly
                var serviceProvider = serviceCollection.BuildServiceProvider();
                // load assembly into MVC and notify of change
                _applicationPartManager.ApplicationParts.Add(new AssemblyPart(storedPlugin.LoadedAssembly));
                _actionDescriptorChangeProvider.TokenSource.Cancel();
                _loadedPlugins.TryAdd(storedPlugin, serviceProvider);
                // finally, register event handlers
                RegisterEventHandlersOnPlugin(serviceProvider);
            }
        }
    }
}
