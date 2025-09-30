import { createContext, useContext, ReactNode } from 'react';
import { useAudioPlayer, AudioPlayer } from 'expo-audio';

const PlayerContext = createContext<AudioPlayer | null>(null);

export function PlayerProvider({ children }: { children: ReactNode }) {
  const player = useAudioPlayer();

  console.log('[PlayerProvider] Player instance ID:', player.id);

  return (
    <PlayerContext.Provider value={player}>
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