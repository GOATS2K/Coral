import { useCallback, useState } from 'react';
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

  const play = useCallback(async (tracks: SimpleTrackDto[], startIndex: number = 0, initializer?: PlaybackInitializer) => {
    console.info('[usePlayer] play called', { player: !!player, trackCount: tracks.length, startIndex });

    if (!player) {
      console.warn('[usePlayer] player is null, cannot play');
      return;
    }

    console.info('[usePlayer] Setting player state...');
    setState({ type: 'setQueue', queue: tracks, index: startIndex, initializer });

    console.info('[usePlayer] Calling loadQueue...');
    await player.loadQueue(tracks, startIndex);
    console.info('[usePlayer] loadQueue completed');
  }, [player, setState]);

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

    // Optimistically update position to avoid visual bounce
    setPlaybackState((prev) => ({
      ...prev,
      position: newPosition
    }));

    player.seekTo(newPosition);
  }, [player, setPlaybackState]);

  const playFromIndex = useCallback(async (index: number) => {
    if (!player || index < 0 || index >= state.queue.length) return;

    setState({ type: 'setCurrentIndex', index });
    await player.playFromIndex(index);
  }, [player, state.queue.length, setState]);

  const setVolume = useCallback((volume: number) => {
    if (!player) return;
    player.setVolume(volume);
  }, [player]);

  const toggleMute = useCallback(() => {
    if (!player) return;
    const newMutedState = !isMuted;
    setIsMuted(newMutedState);
    player.setVolume(newMutedState ? 0 : 1);
  }, [player, isMuted]);

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

// Web doesn't need separate actions hook
export const usePlayerActions = usePlayer;
