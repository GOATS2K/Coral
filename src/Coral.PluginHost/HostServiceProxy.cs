using Microsoft.Extensions.DependencyInjection;

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
            var assemblyName = typeof(TType).Assembly.GetName().Name;
            if (assemblyName != "Coral.Events")
            {
                throw new ArgumentException("You may only access types belonging to the Coral.Events assembly.");
            }
            return scope.ServiceProvider.GetRequiredService<TType>();
        }
    }
}
