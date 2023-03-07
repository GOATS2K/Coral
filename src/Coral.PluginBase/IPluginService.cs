using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.PluginBase
{
    public interface IPluginService
    {
        public void RegisterEventHandlers();
        public void UnregisterEventHandlers();
    }
}
