using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.EventHub
{
    public class TrackPlaybackEventEmitter : TrackPlaybackEvents
    {
        public void EmitEvent()
        {
            EmitPlaybackEvent(new TrackPlaybackEventArgs(123));
        }
    }
}
