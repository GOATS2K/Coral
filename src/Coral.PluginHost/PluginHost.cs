using Coral.Configuration;
using Coral.PluginBase;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;

namespace Coral.PluginHost
{
    public class PluginHost : AssemblyLoadContext
    {
        public IEnumerable<Assembly> LoadPluginAssemblies()
        {
            var assembliesToLoad = Directory.GetFiles(ApplicationConfiguration.Plugins, "*.dll");
            var assemblies = assembliesToLoad.Select(a => LoadFromAssemblyPath(a));
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (typeof(IPlugin).IsAssignableFrom(type))
                    {
                        yield return assembly;
                    }
                }
            }
        }
    }
}