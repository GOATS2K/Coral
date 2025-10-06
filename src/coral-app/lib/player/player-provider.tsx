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
  lastTransitionedIndexRef: MutableRefObject<number>;
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
  const lastTransitionedIndexRef = useRef<number>(-1);
  const lastTransitionTimeRef = useRef<number>(0);
  const inTransitionWindowRef = useRef<boolean>(false);

  const playersRef = useRef<DualPlayerContext>({
    playerA,
    playerB,
    playerATrackIdRef,
    playerBTrackIdRef,
    lastTransitionedIndexRef,
  });

  // Keep ref updated with current players
  playersRef.current = {
    playerA,
    playerB,
    playerATrackIdRef,
    playerBTrackIdRef,
    lastTransitionedIndexRef,
  };

  const stateRef = useRef(state);
  stateRef.current = state;

  // Adaptive interval-based polling for gapless transitions
  // Slow polling (1s) when far from end, fast polling (25ms) when close
  useEffect(() => {
    let intervalId: ReturnType<typeof setInterval>;
    const pollingRateRef = { current: 1000 }; // Start with slow polling

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
      const timeRemainingLimit = 5.0; // Switch to fast polling at 5s remaining (early enough to guarantee catching transition window)

      // Adaptive polling: switch to fast mode when close to end
      if (activePlayer.playing && !isNaN(timeRemaining) && timeRemaining < timeRemainingLimit && pollingRateRef.current !== 25) {
        pollingRateRef.current = 25;
        clearInterval(intervalId);
        intervalId = setInterval(checkTransition, 25);
        // Don't return - continue with the check immediately
      }

      // Guard against duplicate transitions using time-based debouncing
      // Block any transition attempts within 500ms of the last one
      const now = Date.now();
      const timeSinceLastTransition = now - lastTransitionTimeRef.current;
      const justTransitioned = timeSinceLastTransition < 500;

      // Reset to slow polling when far from end or not playing
      // BUT: Stay in fast polling for 2 seconds after transition to handle short tracks
      // (Otherwise we might miss the near-end window on tracks < 2s long)
      const recentTransition = timeSinceLastTransition < 2000;
      if ((!activePlayer.playing || timeRemaining > timeRemainingLimit) && pollingRateRef.current !== 1000 && !recentTransition) {
        pollingRateRef.current = 1000;
        clearInterval(intervalId);
        intervalId = setInterval(checkTransition, 1000);
        // Don't return - continue with the check
      }

      // Check if track has ended (either naturally or we're very close)
      const trackEnded = !activePlayer.playing && !isNaN(duration) && duration > 0 &&
                        (currentTime >= duration - 0.05); // Within 50ms of end

      // Detect if we missed the near-end window (track ended abruptly)
      if (trackEnded) {
        const missedWindow = pollingRateRef.current === 25;
        console.info('[Track Ended]', {
          currentTime: currentTime.toFixed(3),
          duration: duration.toFixed(3),
          timeRemaining: timeRemaining.toFixed(3),
          index: currentState.currentIndex,
          pollingRate: pollingRateRef.current,
          missedWindow: missedWindow && timeRemaining > 0.1,
        });
      }

      // Reset transition window flag when far from end
      if (timeRemaining > 0.15) {
        inTransitionWindowRef.current = false;
      }

      // Start buffer 100ms before track ends - but ONLY on the first poll that detects it
      // This guarantees we trigger at 75-100ms (first poll) instead of randomly within the window
      // Tight timing to minimize overlap while ensuring enough time for browser audio startup
      const enteredTransitionWindow = activePlayer.playing && !isNaN(timeRemaining) && timeRemaining > 0 && timeRemaining < 0.1;
      const nearEnd = enteredTransitionWindow && !inTransitionWindowRef.current;

      // Mark that we've entered the window to prevent retriggering
      if (enteredTransitionWindow) {
        inTransitionWindowRef.current = true;
      }

      // Log precision when we're close to the end
      if (activePlayer.playing && !isNaN(timeRemaining) && timeRemaining < 0.5 && Math.random() < 0.1) {
        console.info('[Precision Check]', {
          currentTime: activePlayer.currentTime,
          duration: activePlayer.duration,
          timeRemaining: timeRemaining,
          nearEnd,
          enteredWindow: enteredTransitionWindow,
          windowFlag: inTransitionWindowRef.current,
        });
      }

      if (justTransitioned && (nearEnd || trackEnded)) {
        console.info('[Transition] Blocked - too soon after last transition', {
          nearEnd,
          trackEnded,
          timeRemaining: timeRemaining.toFixed(3),
          currentIndex: currentState.currentIndex,
          timeSinceLastTransition: `${timeSinceLastTransition}ms`,
        });
        return;
      }

      if (nearEnd || trackEnded) {
        console.info('[Transition] Condition met', {
          nearEnd,
          trackEnded,
          timeRemaining: timeRemaining.toFixed(3),
          player: currentState.activePlayer,
          index: currentState.currentIndex,
          activePlayerState: {
            playing: activePlayer.playing,
            currentTime: activePlayer.currentTime.toFixed(3),
            duration: activePlayer.duration.toFixed(3),
          },
        });

        if (nearEnd) {
          const bufferPlayerName = currentState.activePlayer === 'A' ? 'B' : 'A';
          const bufferTrackIdRef = bufferPlayerName === 'A' ? playerATrackIdRef : playerBTrackIdRef;
          const activeTrackIdRef = currentState.activePlayer === 'A' ? playerATrackIdRef : playerBTrackIdRef;
          console.info('[Near End] Player status', {
            activePlayer: {
              name: currentState.activePlayer,
              playing: activePlayer.playing,
              duration: activePlayer.duration.toFixed(3),
              currentTime: activePlayer.currentTime.toFixed(3),
              trackIdRef: activeTrackIdRef.current,
            },
            bufferPlayer: {
              name: bufferPlayerName,
              isLoaded: bufferPlayer.isLoaded,
              playing: bufferPlayer.playing,
              duration: bufferPlayer.duration.toFixed(3),
              trackIdRef: bufferTrackIdRef.current,
            },
          });
        }

        // Repeat one: seek to start
        if (currentState.repeat === 'one') {
          lastTransitionedIndexRef.current = currentState.currentIndex;
          lastTransitionTimeRef.current = Date.now();
          inTransitionWindowRef.current = false; // Reset for next loop
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

        if (isSequential && bufferPlayer.isLoaded) {
          // Gapless transition: Use buffer (works for both near-end and track-ended)
          console.info('[Gapless] Starting transition', {
            from: currentState.currentIndex,
            to: nextIndex,
            activePlayer: currentState.activePlayer,
            timeRemaining: timeRemaining.toFixed(3),
            trackEnded,
          });
          lastTransitionedIndexRef.current = currentState.currentIndex;
          lastTransitionTimeRef.current = Date.now();
          inTransitionWindowRef.current = false; // Reset for next track

          // Start buffer player - it will overlap briefly with active player
          if (!bufferPlayer.playing) {
            bufferPlayer.play();
          }
          // Only stop active player if track has already ended
          if (trackEnded) {
            activePlayer.pause();
            activePlayer.seekTo(0);
          }
          // Otherwise let active player finish naturally (true gapless overlap)

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

              const newActivePlayer = currentState.activePlayer === 'A' ? 'B' : 'A';
              console.info('[Gapless] State updated', {
                oldActive: currentState.activePlayer,
                newActive: newActivePlayer,
                oldIndex: currentState.currentIndex,
                newIndex: nextIndex,
              });

              return {
                ...latestState,
                currentIndex: nextIndex,
                currentTrack: freshTrack,
                activePlayer: newActivePlayer,
              };
            });

            // Let active player finish naturally if still playing
            // Otherwise we already stopped it above
          } else {
            // Fallback: Load next track when gapless not possible (buffer not ready)
            console.info('[Fallback] Non-gapless transition', {
              reason: !isSequential ? 'non-sequential' : 'buffer not loaded',
              from: currentState.currentIndex,
              to: nextIndex,
              trackEnded,
              bufferLoaded: bufferPlayer.isLoaded,
            });
            // Mark as transitioned before starting to avoid retries
            lastTransitionedIndexRef.current = currentState.currentIndex;
            lastTransitionTimeRef.current = Date.now();
            inTransitionWindowRef.current = false; // Reset for next track

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

    // Start with slow polling (500ms) - will automatically switch to fast (25ms) when needed
    intervalId = setInterval(checkTransition, 500);
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