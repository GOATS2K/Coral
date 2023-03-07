using Coral.EventHub;
using Coral.PluginHost;
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
        private readonly TrackPlaybackEvents _playbackEvents;

        public LastFmService(ILogger<LastFmService> logger, IHostServiceProxy serviceProxy)
        {
            _logger = logger;
            _playbackEvents = serviceProxy.GetHostService<TrackPlaybackEventEmitter>();
            _playbackEvents.TrackPlaybackEvent += HandleEvent;
        }

        private void HandleEvent(object? sender, TrackPlaybackEventArgs e)
        {
            _logger.LogInformation("Event received in plugin");
        }


        public string HelloWorld()
        {
            _logger.LogInformation("Logged message from loaded plugin assembly");
            return "Hello world from LastFMService";
        }
    }
}
