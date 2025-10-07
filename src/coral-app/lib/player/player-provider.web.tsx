import { createContext, useContext, ReactNode, useState, useEffect } from 'react';
import { WebAudioPlayer } from './web-audio-player';

export interface WebPlayerContext {
  player: WebAudioPlayer | null;
}

const PlayerContext = createContext<WebPlayerContext | null>(null);

export function PlayerProvider({ children }: { children: ReactNode }) {
  const [player, setPlayer] = useState<WebAudioPlayer | null>(null);

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
