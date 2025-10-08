import { useEffect, useState, useCallback, useRef } from 'react';
import { useAtom } from 'jotai';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { playerStateAtom, PlaybackInitializer } from '@/lib/state';
import { usePlayerContext } from './player-context';
import type { WebPlayerContext } from './player-provider.web';

export function usePlayer() {
  const { player } = usePlayerContext() as WebPlayerContext;
  const [state, setState] = useAtom(playerStateAtom);
  const [position, setPosition] = useState(0);
  const [duration, setDuration] = useState(0);
  const [isMuted, setIsMuted] = useState(false);
  const [isPlaying, setIsPlaying] = useState(false);
  const lastQueueRef = useRef<SimpleTrackDto[]>([]); // Track last synced queue

  // Poll player state for time updates (Web Audio doesn't have timeupdate events)
  useEffect(() => {
    if (!player) return;

    const interval = setInterval(() => {
      if (player.getIsPlaying()) {
        setPosition(player.getCurrentTime());
        setDuration(player.getDuration());
        setIsPlaying(true);
      } else {
        setIsPlaying(false);
      }
    }, 100); // Update 10 times per second

    // Set up track change callback to update UI state
    player.setTrackChangeCallback((newIndex: number) => {
      console.info('[usePlayer] Track changed to index:', newIndex);
      setState((prevState) => {
        const newTrack = prevState.queue[newIndex];
        if (newTrack) {
          console.info('[usePlayer] Updating state to track:', newTrack.title);
          return { ...prevState, currentIndex: newIndex, currentTrack: newTrack };
        }
        return prevState;
      });
    });

    return () => {
      clearInterval(interval);
    };
  }, [player, setState]);

  // Sync repeat mode with player
  useEffect(() => {
    if (!player) return;
    player.setRepeatMode(state.repeat);
  }, [player, state.repeat]);

  // Sync queue changes to player (shuffle, reorder, etc.)
  useEffect(() => {
    if (!player || state.queue.length === 0) return;

    // Only update if the queue array reference actually changed
    if (lastQueueRef.current !== state.queue) {
      console.info('[usePlayer] Queue changed, syncing to player');
      lastQueueRef.current = state.queue;

      // Update the player's internal queue without restarting playback
      player.updateQueue(state.queue, state.currentIndex);
    }
  }, [player, state.queue, state.currentIndex]);

  const play = async (tracks: SimpleTrackDto[], startIndex: number = 0, initializer?: PlaybackInitializer) => {
    console.info('[usePlayer] play called', { player: !!player, trackCount: tracks.length, startIndex });

    if (!player) {
      console.warn('[usePlayer] player is null, cannot play');
      return;
    }

    const track = tracks[startIndex];
    setPosition(0);

    console.info('[usePlayer] Setting player state...');
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

    // Update ref to prevent duplicate sync in useEffect
    lastQueueRef.current = tracks;

    // Synchronously update repeat mode before loading queue to prevent race condition
    // where a track might end with stale repeat mode before the effect updates it
    player.setRepeatMode('off');

    console.info('[usePlayer] Calling loadQueue...');
    await player.loadQueue(tracks, startIndex);
    console.info('[usePlayer] loadQueue completed');
  };

  const togglePlayPause = useCallback(async () => {
    if (!player) return;
    await player.togglePlayPause();
  }, [player]);

  const skip = useCallback(async (direction: 1 | -1) => {
    if (!player) return;

    // Player handles repeat mode logic internally
    await player.skip(direction);

    // State will be updated via trackChangeCallback
  }, [player]);

  const seekTo = useCallback(async (newPosition: number) => {
    if (!player) return;
    player.seekTo(newPosition);
    setPosition(newPosition);
  }, [player]);

  const playFromIndex = async (index: number) => {
    if (!player || index < 0 || index >= state.queue.length) return;

    const track = state.queue[index];
    setPosition(0);

    setState({ ...state, currentIndex: index, currentTrack: track });

    await player.playFromIndex(index);
  };

  const setVolume = (volume: number) => {
    if (!player) return;
    player.setVolume(volume);
  };

  const toggleMute = () => {
    if (!player) return;
    const newMutedState = !isMuted;
    setIsMuted(newMutedState);
    // Web Audio doesn't have mute, just set volume to 0
    player.setVolume(newMutedState ? 0 : 1);
  };

  return {
    activeTrack: state.currentTrack,
    isPlaying,
    progress: { position, duration },
    queue: state.queue,
    currentIndex: state.currentIndex,
    volume: player?.getVolume() || 1,
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

// Web doesn't need separate actions hook
export const usePlayerActions = usePlayer;
