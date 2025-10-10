import { useAudioPlayerStatus } from 'expo-audio';
import { useAtom, useSetAtom } from 'jotai';
import { useEffect, useState, useCallback } from 'react';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { playerStateAtom, PlaybackInitializer } from '@/lib/state';
import { useNativePlayerContext } from './player-provider.native';
import { loadTrack, waitForPlayerLoaded } from './player-utils';

export function usePlayer() {
  const { playerA, playerB, playerATrackIdRef, playerBTrackIdRef, lastTransitionedIndexRef } = useNativePlayerContext();
  const [state, setState] = useAtom(playerStateAtom);
  const [position, setPosition] = useState(0);
  const [isMuted, setIsMuted] = useState(false);

  const activePlayer = state.activePlayer === 'A' ? playerA : playerB;
  const bufferPlayer = state.activePlayer === 'A' ? playerB : playerA;

  const status = useAudioPlayerStatus(activePlayer);
  const bufferStatus = useAudioPlayerStatus(bufferPlayer);

  const play = async (tracks: SimpleTrackDto[], startIndex: number = 0, initializer?: PlaybackInitializer) => {
    const track = tracks[startIndex];
    setPosition(0);

    // Reset transition tracking on manual play
    lastTransitionedIndexRef.current = -1;

    playerA.pause();
    playerB.pause();
    playerA.seekTo(0);
    playerB.seekTo(0);

    setState({
      queue: tracks,
      currentIndex: startIndex,
      currentTrack: track,
      activePlayer: 'A',
      repeat: 'off',
      isShuffled: false,
      originalQueue: null,
      initializer: initializer || null,
    });
    loadTrack(playerA, track.id);
    playerATrackIdRef.current = track.id;
    await waitForPlayerLoaded(playerA);
    playerA.play();

    const nextTrack = tracks[startIndex + 1];
    if (nextTrack) {
      loadTrack(playerB, nextTrack.id);
      playerBTrackIdRef.current = nextTrack.id;
    }
  };

  const togglePlayPause = useCallback(() => {
    if (status.playing) {
      activePlayer.pause();
    } else {
      activePlayer.play();
    }
  }, [status.playing, activePlayer]);

  const skip = useCallback(async (direction: 1 | -1) => {
    setPosition(0);

    // Reset transition tracking on manual skip
    lastTransitionedIndexRef.current = -1;

    let newIndex = state.currentIndex + direction;

    // Wrap around with repeat-all
    if (newIndex < 0) {
      if (state.repeat !== 'all') return;
      newIndex = state.queue.length - 1;
    } else if (newIndex >= state.queue.length) {
      if (state.repeat !== 'all') return;
      newIndex = 0;
    }

    const track = state.queue[newIndex];
    const currentActive = state.activePlayer === 'A' ? playerA : playerB;
    const currentBuffer = state.activePlayer === 'A' ? playerB : playerA;

    // Gapless forward skip if buffer has next track ready (sequential or wrap-around)
    const isSequential = (direction === 1 && newIndex === state.currentIndex + 1) ||
                        (direction === 1 && state.currentIndex === state.queue.length - 1 && newIndex === 0);
    const bufferReady = isSequential && bufferStatus.isLoaded && currentBuffer === bufferPlayer;

    if (bufferReady) {
      currentActive.pause();
      currentActive.seekTo(0);
      currentBuffer.play();

      const newActivePlayer = state.activePlayer === 'A' ? 'B' : 'A';

      setState({
        ...state,
        currentIndex: newIndex,
        currentTrack: track,
        activePlayer: newActivePlayer,
      });

      // Load next track with wrapping
      let nextIndex = newIndex + 1;
      if (nextIndex >= state.queue.length && state.repeat === 'all') {
        nextIndex = 0;
      }
      const nextTrack = state.queue[nextIndex];
      if (nextTrack) {
        loadTrack(currentActive, nextTrack.id);
        // currentActive becomes the new buffer after swap
        if (state.activePlayer === 'A') {
          playerATrackIdRef.current = nextTrack.id;
        } else {
          playerBTrackIdRef.current = nextTrack.id;
        }
      }
      return;
    }

    // Backward skip - load on buffer player
    if (direction === -1) {
      currentActive.pause();
      currentActive.seekTo(0);
      loadTrack(currentBuffer, track.id);
      // currentBuffer becomes the new active after swap
      if (state.activePlayer === 'A') {
        playerBTrackIdRef.current = track.id;
      } else {
        playerATrackIdRef.current = track.id;
      }

      setState({
        ...state,
        currentIndex: newIndex,
        currentTrack: track,
        activePlayer: state.activePlayer === 'A' ? 'B' : 'A',
      });

      try {
        await waitForPlayerLoaded(currentBuffer);
        currentBuffer.play();

        // Load next track into buffer (playback always continues forward)
        let nextIndex = newIndex + 1;
        if (nextIndex >= state.queue.length && state.repeat === 'all') {
          nextIndex = 0;
        }
        const nextTrack = nextIndex < state.queue.length ? state.queue[nextIndex] : null;
        if (nextTrack) {
          loadTrack(currentActive, nextTrack.id);
          // currentActive becomes the new buffer after swap
          if (state.activePlayer === 'A') {
            playerATrackIdRef.current = nextTrack.id;
          } else {
            playerBTrackIdRef.current = nextTrack.id;
          }
        }
      } catch (err) {
        console.error('Skip error:', err);
      }
      return;
    }

    // Forward skip without buffer - load on active player
    loadTrack(currentActive, track.id);
    // Update ref for active player (stays the same, no swap)
    if (state.activePlayer === 'A') {
      playerATrackIdRef.current = track.id;
    } else {
      playerBTrackIdRef.current = track.id;
    }

    setState({
      ...state,
      currentIndex: newIndex,
      currentTrack: track,
    });

    try {
      await waitForPlayerLoaded(currentActive);
      currentActive.play();

      // Load next track with wrapping
      let nextIndex = newIndex + 1;
      if (nextIndex >= state.queue.length && state.repeat === 'all') {
        nextIndex = 0;
      }
      const nextTrack = state.queue[nextIndex];
      if (nextTrack) {
        loadTrack(currentBuffer, nextTrack.id);
        // Update ref for buffer player
        if (state.activePlayer === 'A') {
          playerBTrackIdRef.current = nextTrack.id;
        } else {
          playerATrackIdRef.current = nextTrack.id;
        }
      }
    } catch (err) {
      console.error('Skip error:', err);
    }
  }, [state, playerA, playerB, bufferPlayer, bufferStatus, setState, playerATrackIdRef, playerBTrackIdRef, lastTransitionedIndexRef]);

  const seekTo = useCallback(async (newPosition: number) => {
    setPosition(newPosition);
    // Reset transition tracking on seek (user might seek past the transition point)
    lastTransitionedIndexRef.current = -1;
    await activePlayer.seekTo(newPosition);
  }, [activePlayer, lastTransitionedIndexRef]);


  const playFromIndex = async (index: number) => {
    if (index < 0 || index >= state.queue.length) return;

    const track = state.queue[index];
    setPosition(0);

    // Reset transition tracking on manual track selection
    lastTransitionedIndexRef.current = -1;

    activePlayer.pause();
    bufferPlayer.pause();
    activePlayer.seekTo(0);
    bufferPlayer.seekTo(0);

    setState({ ...state, currentIndex: index, currentTrack: track, activePlayer: 'A' });
    loadTrack(playerA, track.id);
    playerATrackIdRef.current = track.id;
    await waitForPlayerLoaded(playerA);
    playerA.play();

    const nextTrack = state.queue[index + 1];
    if (nextTrack) {
      loadTrack(playerB, nextTrack.id);
      playerBTrackIdRef.current = nextTrack.id;
    }
  };

  const setVolume = (volume: number) => {
    activePlayer.volume = volume;
  };

  const toggleMute = () => {
    const newMutedState = !isMuted;
    setIsMuted(newMutedState);
    activePlayer.muted = newMutedState;
    bufferPlayer.muted = newMutedState;
  };


  // Sync position with actual player position
  useEffect(() => {
    if (!isNaN(status.currentTime)) {
      setPosition(status.currentTime);
    }
  }, [status.currentTime]);

  // Validation: check currentTrack matches queue[currentIndex]
  useEffect(() => {
    if (state.queue.length > 0 && state.currentTrack) {
      const expectedTrack = state.queue[state.currentIndex];
      if (expectedTrack && expectedTrack.id !== state.currentTrack.id) {
        console.error('❌ [Player] State inconsistency detected!', {
          currentTrackId: state.currentTrack.id,
          currentTrackTitle: state.currentTrack.title,
          expectedTrackId: expectedTrack.id,
          expectedTrackTitle: expectedTrack.title,
          currentIndex: state.currentIndex,
          queueLength: state.queue.length,
        });
      }
    }
  }, [state.currentTrack, state.currentIndex, state.queue]);

  // Update buffer player when queue or current index changes
  useEffect(() => {
    let nextIndex = state.currentIndex + 1;

    // Handle repeat-all wrapping
    if (nextIndex >= state.queue.length) {
      if (state.repeat === 'all') {
        nextIndex = 0;
      } else {
        return; // No next track
      }
    }

    const nextTrack = state.queue[nextIndex];
    if (nextTrack) {
      const bufferPlayerName = state.activePlayer === 'A' ? 'B' : 'A';
      const bufferTrackIdRef = bufferPlayerName === 'A' ? playerATrackIdRef : playerBTrackIdRef;

      // Skip if buffer already has this track
      if (bufferTrackIdRef.current === nextTrack.id) {
        return;
      }

      // CRITICAL: Don't load into buffer if it's still playing the previous track
      // This happens during gapless transitions when we swap active/buffer
      // The old active player needs time to finish before we can load the next track
      if (bufferPlayer.playing) {
        const timeRemaining = bufferPlayer.duration - bufferPlayer.currentTime;
        // Retry loading after the track should be finished
        const retryDelay = Math.max((timeRemaining * 1000) + 100, 500);
        setTimeout(() => {
          loadTrack(bufferPlayer, nextTrack.id);
          bufferTrackIdRef.current = nextTrack.id;
        }, retryDelay);
        return;
      }

      loadTrack(bufferPlayer, nextTrack.id);
      bufferTrackIdRef.current = nextTrack.id;
    }
  }, [state.queue, state.currentIndex, state.activePlayer, state.repeat, bufferPlayer, playerATrackIdRef, playerBTrackIdRef]);

  return {
    activeTrack: state.currentTrack,
    isPlaying: status.playing,
    progress: { position, duration: status.duration },
    queue: state.queue,
    currentIndex: state.currentIndex,
    volume: activePlayer.volume,
    isMuted,
    repeat: state.repeat,
    isShuffled: state.isShuffled,
    play,
    togglePlayPause,
    skip,
    seekTo,
    setVolume,
    toggleMute,
    playFromIndex,
  };
}

// Hook that returns only player actions without subscribing to status updates
// Use this in components that only need to trigger actions, not display player state
export function usePlayerActions() {
  const { playerA, playerB, playerATrackIdRef, playerBTrackIdRef, lastTransitionedIndexRef } = useNativePlayerContext();
  const setState = useSetAtom(playerStateAtom);

  const play = useCallback(async (tracks: SimpleTrackDto[], startIndex: number = 0, initializer?: PlaybackInitializer) => {
    const track = tracks[startIndex];

    // Reset transition tracking on manual play
    lastTransitionedIndexRef.current = -1;

    playerA.pause();
    playerB.pause();
    playerA.seekTo(0);
    playerB.seekTo(0);

    setState({
      queue: tracks,
      currentIndex: startIndex,
      currentTrack: track,
      activePlayer: 'A',
      repeat: 'off',
      isShuffled: false,
      originalQueue: null,
      initializer: initializer || null,
    });
    loadTrack(playerA, track.id);
    playerATrackIdRef.current = track.id;
    await waitForPlayerLoaded(playerA);
    playerA.play();

    const nextTrack = tracks[startIndex + 1];
    if (nextTrack) {
      loadTrack(playerB, nextTrack.id);
      playerBTrackIdRef.current = nextTrack.id;
    }
  }, [playerA, playerB, playerATrackIdRef, playerBTrackIdRef, lastTransitionedIndexRef, setState]);

  return { play };
}