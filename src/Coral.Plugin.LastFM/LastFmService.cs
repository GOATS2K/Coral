using Microsoft.Extensions.Logging;
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
        private readonly ILogger<LastFmService> _logger;

        public LastFmService(ILogger<LastFmService> logger)
        {
            _logger = logger;
        }

        public string HelloWorld()
        {
            _logger.LogInformation("Logged message from loaded plugin assembly");
            return "Hello world from LastFMService";
        }
    }
}
