using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Plugin.LastFM
{
    public interface ILastFmService
    {
        public string HelloWorld();
    }
    public class LastFmService : ILastFmService
    {
        public string HelloWorld()
        {
            return "Hello world from LastFMService";
        }
    }
}
