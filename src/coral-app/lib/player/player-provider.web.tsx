import { createContext, useContext, ReactNode, useState, useEffect } from 'react';
import { useAtom, useSetAtom } from 'jotai';
import { WebAudioPlayer, PlayerEventNames } from './web-audio-player';
import { playerStateAtom, playbackStateAtom } from '@/lib/state';

export interface WebPlayerContext {
  player: WebAudioPlayer | null;
}

const PlayerContext = createContext<WebPlayerContext | null>(null);

export function PlayerProvider({ children }: { children: ReactNode }) {
  const [player, setPlayer] = useState<WebAudioPlayer | null>(null);
  const [state, setState] = useAtom(playerStateAtom);
  const setPlaybackState = useSetAtom(playbackStateAtom);

  // Initialize Web Audio Player
  useEffect(() => {
    console.info('[PlayerProvider] Initializing Web Audio Player...');
    let mounted = true;

    const audioPlayer = new WebAudioPlayer();

    if (mounted) {
      console.info('[PlayerProvider] Web Audio Player ready');
      setPlayer(audioPlayer);
    }

    return () => {
      console.info('[PlayerProvider] Cleanup');
      mounted = false;
      if (audioPlayer) {
        audioPlayer.destroy();
      }
    };
  }, []);

  // ONE polling interval for entire app
  useEffect(() => {
    if (!player) return;

    const interval = setInterval(() => {
      const isPlaying = player.getIsPlaying();

      if (isPlaying) {
        const position = player.getCurrentTime();
        const duration = player.getDuration();

        setPlaybackState({
          position,
          duration,
          isPlaying: true
        });

        // Schedule next track when 15s remaining (check once in 14.5-15s window)
        const remaining = duration - position;
        if (remaining > 0 && remaining <= 15 && remaining > 14.5) {
          player.checkAndScheduleNext();
        }
      }
    }, 250);

    return () => clearInterval(interval);
  }, [player, setPlaybackState]); // Stable - never re-runs!

  // Atom → Player sync
  useEffect(() => {
    if (!player || state.queue.length === 0) return;
    player.updateQueue(state.queue, state.currentIndex);
  }, [player, state.queue, state.currentIndex]);

  // Sync repeat mode
  useEffect(() => {
    if (!player) return;
    player.setRepeatMode(state.repeat);
  }, [player, state.repeat]);

  // Player → Atom sync (via events)
  useEffect(() => {
    if (!player) return;

    const handleTrackChange = (data: { index: number }) => {
      setState({ type: 'setCurrentIndex', index: data.index });
    };

    const handlePlaybackStateChange = (data: { isPlaying: boolean }) => {
      setPlaybackState((prev) => ({
        ...prev,
        isPlaying: data.isPlaying
      }));
    };

    const handleBufferingStateChange = (data: { isBuffering: boolean }) => {
      setPlaybackState((prev) => ({
        ...prev,
        isBuffering: data.isBuffering
      }));
    };

    player.on(PlayerEventNames.TRACK_CHANGED, handleTrackChange);
    player.on(PlayerEventNames.PLAYBACK_STATE_CHANGED, handlePlaybackStateChange);
    player.on(PlayerEventNames.BUFFERING_STATE_CHANGED, handleBufferingStateChange);

    return () => {
      player.off(PlayerEventNames.TRACK_CHANGED, handleTrackChange);
      player.off(PlayerEventNames.PLAYBACK_STATE_CHANGED, handlePlaybackStateChange);
      player.off(PlayerEventNames.BUFFERING_STATE_CHANGED, handleBufferingStateChange);
    };
  }, [player, setState, setPlaybackState]);

  return (
    <PlayerContext.Provider value={{ player }}>
      {children}
    </PlayerContext.Provider>
  );
}

export function useWebPlayerContext() {
  const context = useContext(PlayerContext);
  if (!context) {
    throw new Error('useWebPlayerContext must be used within PlayerProvider');
  }
  return context;
}

// Alias for native compatibility
export const useNativePlayerContext = useWebPlayerContext;
