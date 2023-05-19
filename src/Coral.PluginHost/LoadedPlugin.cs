using Coral.PluginBase;
using System.Reflection;

namespace Coral.PluginHost
{
    public class LoadedPlugin
    {
        public IPlugin Plugin { get; set; } = null!;
        public Assembly LoadedAssembly { get; set; } = null!;
        public PluginLoader PluginLoader { get; set; } = null!;
        public PluginBaseController PluginController { get; set; } = default!;
    }
}