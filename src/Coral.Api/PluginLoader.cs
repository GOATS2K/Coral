using System.Data;
using Coral.Configuration;
using Coral.PluginBase;
using System.Reflection;
using System.Runtime.Loader;

namespace Coral.Api
{
    public class PluginLoader : AssemblyLoadContext
    {
        private readonly ILogger<PluginLoader> _logger;

        public PluginLoader() : base(true)
        {
            var loggerFactory = LoggerFactory.Create(opt => opt.AddConsole());
            _logger = loggerFactory.CreateLogger<PluginLoader>();
        }

        public (Assembly Assembly, IPlugin Plugin)? LoadPluginAssembly(string assemblyPath)
        {
            var assembly = LoadFromAssemblyPath(assemblyPath);
            // if PluginBase is present,
            // the plugins won't load, so raise exception
            if (assembly.GetName().FullName.StartsWith("Coral.PluginBase"))
            {
                _logger.LogWarning("Coral.PluginBase assembly detected, please remove from plugin folder. " +
                                                    $"Skipping load of: {assembly.Location}" +
                                                    " to ensure plug-ins can load.");
                return null;
            }

            var types = assembly.GetTypes();

            // if assembly has more than 1 plugin,
            // throw exception about poor design.
            var pluginCount = types.Count(t => typeof(IPlugin).IsAssignableFrom(t));
            if (pluginCount > 1)
            {
                throw new ConstraintException("Cannot load assembly with more than 1 plugin." +
                                                " Please separate your plugins into multiple assemblies");
            }

            var pluginType = types.Single(t => typeof(IPlugin).IsAssignableFrom(t));
            var plugin = Activator.CreateInstance(pluginType) as IPlugin;
            if (plugin != null)
            {
                _logger.LogInformation("Loaded plugin: {name} - {description}", plugin.Name, plugin.Description);
                return (assembly, plugin);
            }
            return null;
        }

        public IEnumerable<Assembly> LoadPluginAssemblies()
        {
            var assembliesToLoad = Directory.GetFiles(ApplicationConfiguration.Plugins, "*.dll");
            var assemblies = assembliesToLoad.Select(LoadFromAssemblyPath);
            foreach (var assembly in assemblies)
            {
                // if PluginBase is present,
                // the plugins won't load, so raise exception
                if (assembly.GetName().FullName.StartsWith("Coral.PluginBase"))
                {
                    _logger.LogWarning("Coral.PluginBase assembly detected, please remove from plugin folder. " +
                                                        $"Skipping load of: {assembly.Location}" +
                                                        " to ensure plug-ins can load.");
                    continue;
                }
                
                var types = assembly.GetTypes();
                
                // if assembly has more than 1 plugin,
                // throw exception about poor design.
                var pluginCount = types.Count(t => typeof(IPlugin).IsAssignableFrom(t));
                if (pluginCount > 1)
                {
                    throw new ConstraintException("Cannot load assembly with more than 1 plugin." +
                                                  " Please separate your plugins into multiple assemblies");
                }
                
                if (pluginCount != 0)
                {
                    var pluginType = types.Single(t => typeof(IPlugin).IsAssignableFrom(t));
                    var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                    if (plugin != null)
                        _logger.LogInformation("Loaded plugin: {name} - {description}", plugin.Name, plugin.Description);
                    yield return assembly;
                }
            }
        }
    }
}