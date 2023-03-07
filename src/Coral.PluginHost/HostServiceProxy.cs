using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.PluginHost
{
    public interface IHostServiceProxy
    {
        public TType GetHostService<TType>()
            where TType : class;
    }

    public class HostServiceProxy: IHostServiceProxy
    {
        private readonly IServiceProvider _serviceProvider;

        public HostServiceProxy(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public TType GetHostService<TType>()
            where TType : class
        {
            using var scope = _serviceProvider.CreateScope();
            return scope.ServiceProvider.GetRequiredService<TType>();
        }
    }
}
