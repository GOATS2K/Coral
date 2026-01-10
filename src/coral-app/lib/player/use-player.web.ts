import { useState } from 'react';
import { useAtom, useAtomValue, useSetAtom } from 'jotai';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { playerStateAtom, playbackStateAtom, PlaybackInitializer } from '@/lib/state';
import { usePlayerContext } from './player-context';
import type { WebPlayerContext } from './player-provider.web';

export function usePlayer() {
  const { player } = usePlayerContext() as WebPlayerContext;
  const [state, setState] = useAtom(playerStateAtom);
  const playbackState = useAtomValue(playbackStateAtom);
  const setPlaybackState = useSetAtom(playbackStateAtom);
  const [isMuted, setIsMuted] = useState(false);

  // Derive current track
  const currentTrack = state.queue[state.currentIndex] || null;

  const play = async (tracks: SimpleTrackDto[], startIndex: number = 0, initializer?: PlaybackInitializer) => {
    if (!player) {
      console.warn('[usePlayer] player is null, cannot play');
      return;
    }

    setState({ type: 'setQueue', queue: tracks, index: startIndex, initializer });
    await player.loadQueue(tracks, startIndex);
  };

  const togglePlayPause = async () => {
    if (!player) return;
    await player.togglePlayPause();
  };

  const skip = async (direction: 1 | -1) => {
    if (!player) return;

    // Player handles repeat mode logic internally
    await player.skip(direction);

    // State will be updated via trackChangeCallback
  };

  const seekTo = async (newPosition: number) => {
    if (!player) return;

    // Optimistically update position to avoid visual bounce
    setPlaybackState((prev) => ({
      ...prev,
      position: newPosition
    }));

    player.seekTo(newPosition);
  };

  const playFromIndex = async (index: number) => {
    if (!player || index < 0 || index >= state.queue.length) return;

    setState({ type: 'setCurrentIndex', index });
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
    player.setVolume(newMutedState ? 0 : 1);
  };

  return {
    // From playerStateAtom
    activeTrack: currentTrack,
    queue: state.queue,
    currentIndex: state.currentIndex,
    repeat: state.repeat,
    isShuffled: state.isShuffled,

    // From playbackStateAtom
    isPlaying: playbackState.isPlaying,
    progress: {
      position: playbackState.position,
      duration: playbackState.duration
    },

    // UI-only state
    volume: player?.getVolume() || 1,
    isMuted,

    // Actions
    play,
    togglePlayPause,
    skip,
    seekTo,
    setVolume,
    toggleMute,
    playFromIndex,
  };
}

// Actions-only hook - no state subscriptions, prevents unnecessary re-renders
export function usePlayerActions() {
  const { player } = usePlayerContext() as WebPlayerContext;
  const setState = useSetAtom(playerStateAtom);
  const setPlaybackState = useSetAtom(playbackStateAtom);

  const play = async (tracks: SimpleTrackDto[], startIndex: number = 0, initializer?: PlaybackInitializer) => {
    if (!player) {
      console.warn('[usePlayerActions] player is null, cannot play');
      return;
    }

    setState({ type: 'setQueue', queue: tracks, index: startIndex, initializer });
    await player.loadQueue(tracks, startIndex);
    if (!player.getIsPlaying()) {
      player.togglePlayPause();
    }
  };

  const togglePlayPause = async () => {
    if (!player) return;
    await player.togglePlayPause();
  };

  const skip = async (direction: 1 | -1) => {
    if (!player) return;
    await player.skip(direction);
  };

  const seekTo = async (newPosition: number) => {
    if (!player) return;

    // Optimistically update position to avoid visual bounce
    setPlaybackState((prev) => ({
      ...prev,
      position: newPosition
    }));

    player.seekTo(newPosition);
  };

  const playFromIndex = async (index: number) => {
    if (!player) return;
    // Can't check queue length without subscribing to state
    setState({ type: 'setCurrentIndex', index });
    await player.playFromIndex(index);
  };

  const setVolume = (volume: number) => {
    if (!player) return;
    player.setVolume(volume);
  };

  const toggleMute = () => {
    if (!player) return;
    const currentVolume = player.getVolume();
    player.setVolume(currentVolume > 0 ? 0 : 1);
  };

  return {
    play,
    togglePlayPause,
    skip,
    seekTo,
    setVolume,
    toggleMute,
    playFromIndex,
  };
}
