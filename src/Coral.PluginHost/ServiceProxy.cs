using Coral.PluginBase;

namespace Coral.PluginHost
{
    public class ServiceProxy : IServiceProxy
    {
        private readonly IPluginContext _context;

        public ServiceProxy(IPluginContext context)
        {
            _context = context;
        }

        public TType GetService<TType>()
            where TType : class
        {
            return _context.GetService<TType>();
        }
    }
}
