import { createContext, useContext, ReactNode, useRef, useEffect } from 'react';
import { useAudioPlayer, AudioPlayer } from 'expo-audio';
import { useAtom } from 'jotai';
import { playerStateAtom } from '@/lib/state';
import { loadTrack } from './player-utils';

interface DualPlayerContext {
  playerA: AudioPlayer;
  playerB: AudioPlayer;
}

const PlayerContext = createContext<DualPlayerContext | null>(null);

// Singleton player instances to persist across navigation
let globalPlayerA: AudioPlayer | null = null;
let globalPlayerB: AudioPlayer | null = null;

export function PlayerProvider({ children }: { children: ReactNode }) {
  const newPlayerA = useAudioPlayer();
  const newPlayerB = useAudioPlayer();
  const [state, setState] = useAtom(playerStateAtom);

  const playersRef = useRef<DualPlayerContext | null>(null);

  if (!globalPlayerA) globalPlayerA = newPlayerA;
  if (!globalPlayerB) globalPlayerB = newPlayerB;

  playersRef.current = {
    playerA: globalPlayerA,
    playerB: globalPlayerB,
  };

  const playerA = globalPlayerA;
  const playerB = globalPlayerB;

  const stateRef = useRef(state);
  stateRef.current = state;

  const lastTransitionedRef = useRef({ player: '', index: -1 });

  // High-frequency polling for precise gapless transitions
  useEffect(() => {
    let rafId: number;

    const checkTransition = () => {
      const currentState = stateRef.current;
      const activePlayer = currentState.activePlayer === 'A' ? playerA : playerB;
      const bufferPlayer = currentState.activePlayer === 'A' ? playerB : playerA;

      const timeRemaining = activePlayer.duration - activePlayer.currentTime;
      const nearEnd = !isNaN(timeRemaining) && timeRemaining > 0 && timeRemaining < 0.1;

      if (nearEnd) {
        const alreadyTransitioned =
          lastTransitionedRef.current.player === currentState.activePlayer &&
          lastTransitionedRef.current.index === currentState.currentIndex;

        if (!alreadyTransitioned) {
          const nextIndex = currentState.currentIndex + 1;

          if (nextIndex < currentState.queue.length && bufferPlayer.isLoaded) {
            lastTransitionedRef.current = { player: currentState.activePlayer, index: currentState.currentIndex };

            const nextTrack = currentState.queue[nextIndex];

            // Start buffer (overlap with active for gapless)
            bufferPlayer.play();

            // Update state
            setState({
              ...currentState,
              currentIndex: nextIndex,
              currentTrack: nextTrack,
              activePlayer: currentState.activePlayer === 'A' ? 'B' : 'A',
            });

            // Stop active player after it finishes
            setTimeout(() => {
              activePlayer.pause();
              activePlayer.seekTo(0);

              // Pre-load following track
              const followingTrack = currentState.queue[nextIndex + 1];
              if (followingTrack) {
                loadTrack(activePlayer, followingTrack.id);
              }
            }, 150);
          }
        }
      }

      rafId = requestAnimationFrame(checkTransition);
    };

    rafId = requestAnimationFrame(checkTransition);
    return () => cancelAnimationFrame(rafId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [playerA.id, playerB.id]);

  return (
    <PlayerContext.Provider value={playersRef.current}>
      {children}
    </PlayerContext.Provider>
  );
}

export function useDualPlayers() {
  const players = useContext(PlayerContext);
  if (!players) {
    throw new Error('useDualPlayers must be used within PlayerProvider');
  }
  return players;
}