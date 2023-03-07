using Coral.Dto.Models;
using Coral.EventHub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Services
{
    public interface IPlaybackService
    {
        public void RegisterPlayback(TrackDto track);
    }

    public class PlaybackService : IPlaybackService
    {
        private readonly TrackPlaybackEventEmitter _playbackEventEmitter;

        public PlaybackService(TrackPlaybackEventEmitter playbackEventEmitter)
        {
            _playbackEventEmitter = playbackEventEmitter;
        }

        public void RegisterPlayback(TrackDto track)
        {
            _playbackEventEmitter.EmitEvent(track);
        }
    }
}
