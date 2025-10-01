import { createContext, useContext, ReactNode, useRef } from 'react';
import { useAudioPlayer, AudioPlayer } from 'expo-audio';

const PlayerContext = createContext<AudioPlayer | null>(null);

// Singleton player instance to persist across navigation
let globalPlayerInstance: AudioPlayer | null = null;

export function PlayerProvider({ children }: { children: ReactNode }) {
  // Always call the hook at the top level
  const newPlayer = useAudioPlayer();

  // Use the global instance if it exists, otherwise use the new one
  const playerRef = useRef<AudioPlayer | null>(null);

  if (!globalPlayerInstance) {
    globalPlayerInstance = newPlayer;
  }

  playerRef.current = globalPlayerInstance;

  return (
    <PlayerContext.Provider value={playerRef.current}>
      {children}
    </PlayerContext.Provider>
  );
}

export function useAudioPlayerInstance() {
  const player = useContext(PlayerContext);
  if (!player) {
    throw new Error('useAudioPlayerInstance must be used within PlayerProvider');
  }
  return player;
}