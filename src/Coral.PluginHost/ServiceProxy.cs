<<<<<<< HEAD
﻿using Coral.PluginBase;

namespace Coral.PluginHost
=======
﻿namespace Coral.PluginHost
>>>>>>> chore: remove un-used imports and create editorconfig
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
