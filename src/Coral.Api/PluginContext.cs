using Coral.Configuration;
using Coral.PluginBase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Collections.Concurrent;
using System.Reflection;

namespace Coral.Api
{
    public interface IPluginContext
    {
        public void UnloadAll();
        public void UnloadPlugin(LoadedPlugin plugin);
        public void LoadAssemblies();
        public string LoadedParts();
        public ActionResult? LoadRoute(string controllerName, string? routeName);
    }

    public class PluginContext : IPluginContext
    {
        private readonly ConcurrentDictionary<LoadedPlugin, ServiceProvider> _loadedPlugins = new();
        private readonly ILogger<PluginContext> _logger;
        private readonly ApplicationPartManager _applicationPartManager;


        public PluginContext(ApplicationPartManager applicationPartManager, ILogger<PluginContext> logger)
        {
            _applicationPartManager = applicationPartManager;
            _logger = logger;
        }

        public ActionResult? LoadRoute(string controllerName, string? routeName)
        {
            var targetPlugin = _loadedPlugins.Keys.Where(k => k.PluginController.GetType().Name.ToLower().StartsWith(controllerName.ToLower())).FirstOrDefault();
            if (targetPlugin == null) { return null; }
            var controller = targetPlugin.PluginController;
            // use reflection to get route attribute with name
            var targetMethod = controller.GetType()
                .GetMethods()
                .SelectMany(m => m.GetCustomAttributes<RouteAttribute>(), (MethodInfo method, RouteAttribute attribute) =>
                {
                    if (attribute.Template.ToLower().Equals(routeName.ToLower()))
                    {
                        return method;
                    }
                    return null;
                })
                .SingleOrDefault();
            if (targetMethod == null) { return null; }
            return (ActionResult)targetMethod.Invoke(controller, new object[0]);
        }

        public string LoadedParts()
        {
            var controllerFeature = new ControllerFeature();
            _applicationPartManager.PopulateFeature(controllerFeature);
            var loadedParts = controllerFeature.Controllers.Select(s => s.Name);
            return string.Join(", ", loadedParts);
        }

        public void UnloadAll()
        {
            foreach (var plugin in _loadedPlugins.Keys)
            {
                UnloadPlugin(plugin);
            }
            _logger.LogInformation("Unloading all plugin controllers.");
        }

        public void UnloadPlugin(LoadedPlugin plugin)
        {
            if (plugin == null) return;

            _logger.LogInformation("Unloading plugin: {PluginName}", plugin.Plugin.Name);

            _loadedPlugins.Remove(plugin, out _);
            plugin.PluginLoader.Unload();
        }

        public void LoadAssemblies()
        {
            // load plugin via PluginLoader
            var assembliesToLoad = Directory.GetFiles(ApplicationConfiguration.Plugins, "*.dll");
            foreach (var assemblyToLoad in assembliesToLoad)
            {
                var pluginLoader = new PluginLoader();
                var loadedPlugin = pluginLoader.LoadPluginAssembly(assemblyToLoad);
                if (!loadedPlugin.HasValue)
                {
                    continue;
                }

                // set up servicecollection
                var serviceCollection = new ServiceCollection();
                // run ConfigureServices with new service collection        
                loadedPlugin.Value.Plugin.ConfigureServices(serviceCollection);
                // build ServiceProvider for plugin

                var storedPlugin = new LoadedPlugin()
                {
                    LoadedAssembly = loadedPlugin.Value.Assembly,
                    Plugin = loadedPlugin.Value.Plugin,
                    PluginLoader = pluginLoader
                };

                // register controller with main application
                // get controller from plugin
                var controller = loadedPlugin.Value.Assembly.GetTypes().SingleOrDefault(t => t.IsSubclassOf(typeof(PluginBaseController)));
                if (controller == null)
                {
                    return;
                }
                // load controller assembly
                serviceCollection.AddScoped(controller);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                storedPlugin.PluginController = (PluginBaseController)serviceProvider.GetRequiredService(controller);
                _loadedPlugins.TryAdd(storedPlugin, serviceProvider);
            }
        }
    }
}
