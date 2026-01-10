import { createContext, useContext, ReactNode, useState, useEffect } from 'react';
import { useAtom, useSetAtom } from 'jotai';
import { MSEWebAudioPlayer } from './mse-web-audio-player';
import { MpvIpcProxy } from './mpv-ipc-proxy';
import type { PlayerBackend } from './player-backend';
import { PlayerEventNames } from './player-backend';
import { playerStateAtom, playbackStateAtom } from '@/lib/state';
import { Config } from '@/lib/config';

export interface WebPlayerContext {
  player: PlayerBackend | null;
}

const PlayerContext = createContext<WebPlayerContext | null>(null);

// Detect if running in Electron
const isElectron = Config.isElectron();

export function PlayerProvider({ children }: { children: ReactNode }) {
  const [player, setPlayer] = useState<PlayerBackend | null>(null);
  const [state, setState] = useAtom(playerStateAtom);
  const setPlaybackState = useSetAtom(playbackStateAtom);

  // Initialize player based on environment
  useEffect(() => {
    const playerType = isElectron ? 'MpvPlayer (Electron)' : 'MSE Web Audio Player';
    console.info(`[PlayerProvider] Initializing ${playerType}...`);
    let mounted = true;
    let playerInstance: PlayerBackend | null = null;

    const initializePlayer = async () => {
      if (isElectron) {
        // Electron: Create MpvIpcProxy and initialize with backend URL from config
        const baseUrl = await Config.getBackendUrl();
        const mpvPlayer = new MpvIpcProxy();
        await mpvPlayer.initialize(baseUrl);
        playerInstance = mpvPlayer;
      } else {
        // Web: Use MSE player (no async initialization needed)
        playerInstance = new MSEWebAudioPlayer();
      }

      if (mounted) {
        console.info(`[PlayerProvider] ${playerType} ready`);
        setPlayer(playerInstance);
      }
    };

    initializePlayer().catch(err => {
      console.error('[PlayerProvider] Failed to initialize player:', err);
    });

    return () => {
      console.info('[PlayerProvider] Cleanup');
      mounted = false;
      if (playerInstance) {
        playerInstance.destroy();
      }
    };
  }, []);

  useEffect(() => {
    if (!player) return;

    const interval = setInterval(() => {
      const isPlaying = player.getIsPlaying();
      const position = player.getCurrentTime();
      const duration = player.getDuration();

      setPlaybackState((prev) => ({
        ...prev,
        position,
        duration,
        isPlaying
      }));
    }, 250);

    return () => clearInterval(interval);
  }, [player, setPlaybackState]);

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
