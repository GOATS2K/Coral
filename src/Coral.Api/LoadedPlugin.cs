using Coral.PluginBase;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using System.Reflection;

namespace Coral.Api
{
    public class LoadedPlugin
    {
        public IPlugin Plugin { get; set; } = null!;
        public Assembly LoadedAssembly { get; set; } = null!;
        public PluginLoader PluginLoader { get; set; } = null!;
        public PluginBaseController PluginController { get; set; }
    }
}