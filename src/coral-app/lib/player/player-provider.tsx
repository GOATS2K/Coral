import { createContext, useContext, ReactNode, useRef, useEffect, MutableRefObject } from 'react';
import { useAudioPlayer, AudioPlayer } from 'expo-audio';
import { useAtom } from 'jotai';
import { playerStateAtom } from '@/lib/state';

interface DualPlayerContext {
  playerA: AudioPlayer;
  playerB: AudioPlayer;
  playerATrackIdRef: MutableRefObject<string | null>;
  playerBTrackIdRef: MutableRefObject<string | null>;
}

const PlayerContext = createContext<DualPlayerContext | null>(null);

export function PlayerProvider({ children }: { children: ReactNode }) {
  const playerA = useAudioPlayer();
  const playerB = useAudioPlayer();
  const [state, setState] = useAtom(playerStateAtom);

  // Track which track ID is loaded in each player
  const playerATrackIdRef = useRef<string | null>(null);
  const playerBTrackIdRef = useRef<string | null>(null);

  const playersRef = useRef<DualPlayerContext>({
    playerA,
    playerB,
    playerATrackIdRef,
    playerBTrackIdRef,
  });

  // Keep ref updated with current players
  playersRef.current = {
    playerA,
    playerB,
    playerATrackIdRef,
    playerBTrackIdRef,
  };

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
          console.log('[TRANSITION] Near end detected', {
            currentIndex: currentState.currentIndex,
            currentTrack: currentState.currentTrack?.title,
            queueLength: currentState.queue.length,
            isShuffled: currentState.isShuffled,
            repeat: currentState.repeat,
            activePlayer: currentState.activePlayer,
          });

          lastTransitionedRef.current = { player: currentState.activePlayer, index: currentState.currentIndex };

          // Repeat one: seek to start
          if (currentState.repeat === 'one') {
            console.log('[TRANSITION] Repeat one - seeking to start');
            activePlayer.seekTo(0);
            return;
          }

          let nextIndex = currentState.currentIndex + 1;

          // Repeat all: wrap to start
          if (nextIndex >= currentState.queue.length) {
            if (currentState.repeat === 'all') {
              nextIndex = 0;
            } else {
              console.log('[TRANSITION] End of queue, no repeat - stopping');
              return; // Stop playback
            }
          }

          const nextTrack = currentState.queue[nextIndex];

          // Check if buffer has the next track ready (sequential or wrap-around)
          const isSequential = nextIndex === currentState.currentIndex + 1 ||
                              (currentState.currentIndex === currentState.queue.length - 1 && nextIndex === 0);

          console.log('[TRANSITION] Checking buffer', {
            nextIndex,
            nextTrack: nextTrack?.title,
            nextTrackId: nextTrack?.id,
            isSequential,
            bufferIsLoaded: bufferPlayer.isLoaded,
            bufferPlayer: currentState.activePlayer === 'A' ? 'B' : 'A',
          });

          if (isSequential && bufferPlayer.isLoaded) {
            console.log('[TRANSITION] ✓ Gapless transition to', nextTrack?.title);

            // Start buffer (overlap with active for gapless)
            bufferPlayer.play();

            // Update ref: buffer player (which becomes active) already has nextTrack loaded
            // This was set by the buffer effect in use-player.ts earlier
            // No need to update refs here - they should already be correct

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
              // Note: buffer loading is now handled by the effect in use-player.ts
            }, 150);
          } else {
            console.log('[TRANSITION] ✗ Cannot transition - buffer not ready', {
              reason: !isSequential ? 'not sequential' : 'buffer not loaded',
            });
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