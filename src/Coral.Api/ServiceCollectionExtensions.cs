using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Api
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPlugins(this IServiceCollection serviceCollection, IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var exportedTypes = assembly.GetExportedTypes();
                // locate types inherting from interfaces and inject pair
                var interfaces = exportedTypes.Where(a => a.IsInterface);
                var pairs = interfaces.ToDictionary(key => key, value => exportedTypes.Where(a => a.IsClass && !a.IsAbstract && a.GetInterfaces().Contains(value)));
                foreach (var (interfaceType, types) in pairs)
                {
                    foreach (var implementingType in types)
                    {
                        serviceCollection.AddScoped(interfaceType, implementingType);
                    }
                }
                // load controller
                serviceCollection.AddMvc().AddApplicationPart(assembly).AddControllersAsServices();
            }
            return serviceCollection;
        }
    }
}
