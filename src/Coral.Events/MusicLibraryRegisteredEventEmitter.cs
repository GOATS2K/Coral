using Coral.Database.Models;
using Coral.Dto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Events
{
    public class MusicLibraryRegisteredEventEmitter
    {
        public event EventHandler<MusicLibraryRegisteredEventArgs> MusicLibraryRegisteredEvent = default!;
        protected virtual void EmitPlaybackEvent(MusicLibraryRegisteredEventArgs e)
        {
            var handler = MusicLibraryRegisteredEvent;
            if (handler != null)
            {
                handler?.Invoke(this, e);
            }
        }

        public void EmitEvent(MusicLibrary library)
        {
            EmitPlaybackEvent(new MusicLibraryRegisteredEventArgs(library));
        }
    }

    public class MusicLibraryRegisteredEventArgs : EventArgs
    {
        public MusicLibrary Library { get; set; }
        public MusicLibraryRegisteredEventArgs(MusicLibrary library)
        {
            Library = library;
        }

    }
}
