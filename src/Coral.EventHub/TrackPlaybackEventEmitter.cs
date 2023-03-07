using Coral.Dto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.EventHub
{
    public class TrackPlaybackEventEmitter
    {
        public event EventHandler<TrackPlaybackEventArgs> TrackPlaybackEvent = default!;
        protected virtual void EmitPlaybackEvent(TrackPlaybackEventArgs e)
        {
            var handler = TrackPlaybackEvent;
            if (handler != null)
            {
                handler?.Invoke(this, e);
            }
        }

        public void EmitEvent(TrackDto track)
        {
            EmitPlaybackEvent(new TrackPlaybackEventArgs(track));
        }
    }

    public class TrackPlaybackEventArgs : EventArgs
    {
        public TrackDto Track { get; set; }
        public TrackPlaybackEventArgs(TrackDto track)
        {
            Track = track;
        }
    }
}
