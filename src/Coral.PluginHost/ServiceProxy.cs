using Coral.PluginHost;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.PluginHost
{
    public interface IServiceProxy
    {
        public TType GetService<TType>()
            where TType : class;
    }

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
