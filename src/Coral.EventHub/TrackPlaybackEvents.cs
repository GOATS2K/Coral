using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.EventHub
{
    public class TrackPlaybackEvents
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
    }

    public class TrackPlaybackEventArgs : EventArgs
    {
        public int TrackId { get; set; }
        public TrackPlaybackEventArgs(int trackId)
        {
            TrackId = trackId;  
        }
    }
}
