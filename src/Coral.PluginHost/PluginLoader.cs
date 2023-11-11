using Coral.PluginBase;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Reflection;
using System.Runtime.Loader;

namespace Coral.PluginHost
{
    public class PluginLoader : AssemblyLoadContext
    {
        private readonly ILogger<PluginLoader> _logger;

        public PluginLoader(ILogger<PluginLoader> logger) : base(true)
        {
            _logger = logger;
        }

        public (Assembly Assembly, IPlugin Plugin)? LoadPluginAssemblies(string assemblyDirectory)
        {
            (Assembly Assembly, IPlugin Plugin)? assemblyGroup = null;
            // we need to load the plugins with their dependencies
            foreach (var assemblyPath in Directory.GetFiles(assemblyDirectory, "*.dll"))
            {
                // if PluginBase is present,
                // the plugins won't load, so raise exception
                if (Path.GetFileName(assemblyPath).StartsWith("Coral.PluginHost"))
                {
                    _logger.LogWarning("Coral.PluginHost assembly detected, please remove from plugin folder. " +
                                                        $"Skipping load of: {assemblyPath}" +
                                                        " to ensure plug-ins can load.");
                    continue;
                }

                var assembly = LoadFromAssemblyPath(assemblyPath);
                try
                {
                    var types = assembly.GetTypes();
                    // if assembly has more than 1 plugin,
                    // throw exception about poor design.
                    var pluginCount = types.Count(t => typeof(IPlugin).IsAssignableFrom(t));
                    if (pluginCount > 1)
                    {
                        throw new ConstraintException("Cannot load assembly with more than 1 plugin." +
                                                        " Please separate your plugins into multiple assemblies");
                    }

                    // if assembly has no plugins, continue, it's a needed dependency
                    if (pluginCount == 0)
                    {
                        _logger.LogDebug("Loaded plugin dependency: {assemblyName}", assembly.GetName().Name);
                        continue;
                    }

                    var pluginType = types.Single(t => typeof(IPlugin).IsAssignableFrom(t));
                    var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                    if (plugin != null)
                    {
                        _logger.LogInformation("Loaded plugin: {name} - {description}", plugin.Name, plugin.Description);
                        assemblyGroup = (assembly, plugin);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger.LogError("Exception thrown loading types for assembly {AssemblyName} with message {Message}", assembly.GetName().Name, ex);
                }
            }
            return assemblyGroup;
        }
    }
}