import { createContext, useContext, ReactNode, useRef, useEffect, MutableRefObject } from 'react';
import { useAudioPlayer, AudioPlayer, setAudioModeAsync } from 'expo-audio';
import { useAtom } from 'jotai';
import { playerStateAtom } from '@/lib/state';
import { loadTrack, waitForPlayerLoaded } from './player-utils';

interface DualPlayerContext {
  playerA: AudioPlayer;
  playerB: AudioPlayer;
  playerATrackIdRef: MutableRefObject<string | null>;
  playerBTrackIdRef: MutableRefObject<string | null>;
  lastTransitionedRef: MutableRefObject<{ player: string; index: number }>;
}

const PlayerContext = createContext<DualPlayerContext | null>(null);

export function PlayerProvider({ children }: { children: ReactNode }) {
  // Hybrid approach: Use useAudioPlayer() for automatic cleanup (hot reload)
  // but capture first instance in ref for stability (gapless playback)
  const hookPlayerA = useAudioPlayer();
  const hookPlayerB = useAudioPlayer();

  const playerARef = useRef<AudioPlayer | null>(null);
  const playerBRef = useRef<AudioPlayer | null>(null);

  // Capture the first player instances from hooks
  if (!playerARef.current) {
    playerARef.current = hookPlayerA;
  }
  if (!playerBRef.current) {
    playerBRef.current = hookPlayerB;
  }

  // Use the stable ref instances for all operations
  const playerA = playerARef.current;
  const playerB = playerBRef.current;
  const [state, setState] = useAtom(playerStateAtom);

  // Note: No manual cleanup needed - useAudioPlayer() handles it automatically
  // This includes hot reload cleanup via useReleasingSharedObject

  // Configure audio mode for background playback
  useEffect(() => {
    setAudioModeAsync({
      playsInSilentMode: true,
      shouldPlayInBackground: true,
    });
  }, []);

  // Track which track ID is loaded in each player
  const playerATrackIdRef = useRef<string | null>(null);
  const playerBTrackIdRef = useRef<string | null>(null);
  const lastTransitionedRef = useRef({ player: '', index: -1 });

  const playersRef = useRef<DualPlayerContext>({
    playerA,
    playerB,
    playerATrackIdRef,
    playerBTrackIdRef,
    lastTransitionedRef,
  });

  // Keep ref updated with current players
  playersRef.current = {
    playerA,
    playerB,
    playerATrackIdRef,
    playerBTrackIdRef,
    lastTransitionedRef,
  };

  const stateRef = useRef(state);
  stateRef.current = state;

  // Interval-based polling for gapless transitions (100ms = 10Hz)
  // Much more CPU-efficient than RAF (60+ Hz) while still precise enough
  useEffect(() => {
    console.log('🎵 [PlayerProvider] Creating interval', new Date().toISOString());

    const checkTransition = () => {
      const currentState = stateRef.current;
      const activePlayer = currentState.activePlayer === 'A' ? playerA : playerB;
      const bufferPlayer = currentState.activePlayer === 'A' ? playerB : playerA;

      // Early return only if no queue
      if (currentState.queue.length === 0) {
        return;
      }

      const timeRemaining = activePlayer.duration - activePlayer.currentTime;
      const currentTime = activePlayer.currentTime;
      const duration = activePlayer.duration;

      // Check if track has ended (either naturally or we're very close)
      const trackEnded = !activePlayer.playing && !isNaN(duration) && duration > 0 &&
                        (currentTime >= duration - 0.05); // Within 50ms of end

      // Start buffer 500ms before track ends - larger window to avoid missing transitions
      // 25ms polling can be delayed by browser, so wider window ensures detection
      // Accounts for browser audio startup latency (~50-100ms)
      const nearEnd = activePlayer.playing && !isNaN(timeRemaining) && timeRemaining > 0 && timeRemaining < 0.185;

      if (nearEnd || trackEnded) {
        if (nearEnd) {
          const bufferPlayerName = currentState.activePlayer === 'A' ? 'B' : 'A';
          const bufferTrackIdRef = bufferPlayerName === 'A' ? playerATrackIdRef : playerBTrackIdRef;
          console.log('⏰ [Near End] Detected at', timeRemaining.toFixed(3), 's remaining', {
            bufferPlayer: {
              isLoaded: bufferPlayer.isLoaded,
              playing: bufferPlayer.playing,
              duration: bufferPlayer.duration,
              trackId: bufferTrackIdRef.current,
            },
          });
        }

        if (trackEnded) {
          console.log('🔚 [Track End] Natural end detected', {
            currentTime,
            duration,
            timeRemaining,
          });
        }

        const alreadyTransitioned =
          !trackEnded && // If track ended, always try to advance
          lastTransitionedRef.current.player === currentState.activePlayer &&
          lastTransitionedRef.current.index === currentState.currentIndex;

        if (alreadyTransitioned) {
          console.log('⚠️ [Already Transitioned] Skipping duplicate transition attempt');
          return;
        }

        // Repeat one: seek to start
        if (currentState.repeat === 'one') {
          lastTransitionedRef.current = { player: currentState.activePlayer, index: currentState.currentIndex };
          activePlayer.seekTo(0);
          return;
        }

        let nextIndex = currentState.currentIndex + 1;

        // Repeat all: wrap to start
        if (nextIndex >= currentState.queue.length) {
          if (currentState.repeat === 'all') {
            nextIndex = 0;
          } else {
            return; // Stop playback
          }
        }

        const nextTrack = currentState.queue[nextIndex];

        // Check if buffer has the next track ready (sequential or wrap-around)
        const isSequential = nextIndex === currentState.currentIndex + 1 ||
                            (currentState.currentIndex === currentState.queue.length - 1 && nextIndex === 0);

        if (isSequential && bufferPlayer.isLoaded && !trackEnded) {
          // Gapless transition: Start buffer (overlap with active)
          console.log('✨ [Gapless] Smooth transition to next track');
          lastTransitionedRef.current = { player: currentState.activePlayer, index: currentState.currentIndex };
          bufferPlayer.play();

            // Update state with fresh track reference from queue to avoid stale objects
            setState((latestState) => {
              const freshTrack = latestState.queue[nextIndex];

              // Defensive check: verify track exists and matches expected ID
              if (!freshTrack) {
                console.warn('⚠️ [Gapless] Track not found at index', nextIndex);
                return latestState;
              }

              if (freshTrack.id !== nextTrack.id) {
                console.warn('⚠️ [Gapless] Track mismatch!', {
                  expected: nextTrack.id,
                  actual: freshTrack.id,
                  index: nextIndex,
                });
              }

              return {
                ...latestState,
                currentIndex: nextIndex,
                currentTrack: freshTrack,
                activePlayer: currentState.activePlayer === 'A' ? 'B' : 'A',
              };
            });

            // Let active player finish naturally - no forced cleanup
            // The track will stop on its own when it reaches the end
          } else {
            // Fallback: Load next track when gapless not possible
            const bufferPlayerName = currentState.activePlayer === 'A' ? 'B' : 'A';
            const bufferTrackIdRef = bufferPlayerName === 'A' ? playerATrackIdRef : playerBTrackIdRef;
            console.log('⏭️ [Fallback] Gapless not available, loading next track normally', {
              reason: trackEnded ? 'Track already ended' : (!isSequential ? 'Not sequential' : 'Buffer not loaded'),
              trackEnded,
              isSequential,
              bufferState: {
                isLoaded: bufferPlayer.isLoaded,
                playing: bufferPlayer.playing,
                duration: bufferPlayer.duration,
                currentTime: bufferPlayer.currentTime,
                trackId: bufferTrackIdRef.current,
                expectedTrackId: nextTrack.id,
              },
            });

            // Mark as transitioned before starting to avoid retries
            lastTransitionedRef.current = { player: currentState.activePlayer, index: currentState.currentIndex };

            // Stop current player and load next track on active player
            activePlayer.pause();
            activePlayer.seekTo(0);

            // Load next track
            loadTrack(activePlayer, nextTrack.id);
            if (currentState.activePlayer === 'A') {
              playerATrackIdRef.current = nextTrack.id;
            } else {
              playerBTrackIdRef.current = nextTrack.id;
            }

            // Update state
            setState((latestState) => ({
              ...latestState,
              currentIndex: nextIndex,
              currentTrack: latestState.queue[nextIndex],
            }));

            // Wait for load and play
            waitForPlayerLoaded(activePlayer).then(() => {
              activePlayer.play();
            }).catch(err => {
              console.error('Failed to load next track:', err);
            });
          }
      }
    };

    // Check every 50ms (20 times per second) for better transition timing accuracy
    const intervalId = setInterval(checkTransition, 25);
    return () => clearInterval(intervalId);
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