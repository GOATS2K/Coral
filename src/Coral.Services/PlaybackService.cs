using Coral.Dto.Models;
using Coral.Events;

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
