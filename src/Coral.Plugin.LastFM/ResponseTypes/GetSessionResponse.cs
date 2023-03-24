using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Plugin.LastFM.ResponseTypes
{
    public class GetSessionResponse
    {
        public Session Session { get; set; } = null!;
    }

    public class Session
    {
        public string Name { get; set; } = null!;
        public string Key { get; set; } = null!;
        public int Subscriber { get; set; }
    }
}
