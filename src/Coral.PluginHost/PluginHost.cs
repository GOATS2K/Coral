using System.Data;
using Coral.Configuration;
using Coral.PluginBase;
using System.Reflection;
using System.Runtime.Loader;

namespace Coral.PluginHost
{
    public class PluginHost : AssemblyLoadContext
    {
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
                    throw new ApplicationException("Coral.PluginBase assembly detected. " +
                                                        $"Please delete the assembly at: {assembly.Location}" +
                                                        $" to ensure plug-ins can load.");
                }
                
                var types = assembly.GetTypes();
                
                // if assembly has more than 1 plugin,
                // throw exception about poor design.
                var pluginCount = types.Count(t => typeof(IPlugin).IsAssignableFrom(t));
                if (pluginCount > 1)
                {
                    throw new ConstraintException("Cannot load assembly with more than 1 plugin.");
                }
                
                if (pluginCount != 0)
                {
                    yield return assembly;
                }
            }
        }
    }
}