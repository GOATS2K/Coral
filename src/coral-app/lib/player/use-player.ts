import { useAudioPlayerStatus } from 'expo-audio';
import { useAtom, useSetAtom } from 'jotai';
import { useEffect, useState, useCallback } from 'react';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { playerStateAtom } from '@/lib/state';
import { useDualPlayers } from './player-provider';
import { getTrackUrl, loadTrack, waitForPlayerLoaded } from './player-utils';
import { fetchRecommendationsForTrack } from '@/lib/client/components';
import { useToast } from '@/lib/hooks/use-toast';

export function usePlayer() {
  const { playerA, playerB } = useDualPlayers();
  const [state, setState] = useAtom(playerStateAtom);
  const [position, setPosition] = useState(0);
  const [isMuted, setIsMuted] = useState(false);
  const { showToast } = useToast();

  const activePlayer = state.activePlayer === 'A' ? playerA : playerB;
  const bufferPlayer = state.activePlayer === 'A' ? playerB : playerA;

  const status = useAudioPlayerStatus(activePlayer);
  const bufferStatus = useAudioPlayerStatus(bufferPlayer);

  const play = async (tracks: SimpleTrackDto[], startIndex: number = 0) => {
    const track = tracks[startIndex];
    setPosition(0);

    playerA.pause();
    playerB.pause();
    playerA.seekTo(0);
    playerB.seekTo(0);

    setState({ queue: tracks, currentIndex: startIndex, currentTrack: track, activePlayer: 'A' });
    loadTrack(playerA, track.id);
    await waitForPlayerLoaded(playerA);
    playerA.play();

    const nextTrack = tracks[startIndex + 1];
    if (nextTrack) {
      loadTrack(playerB, nextTrack.id);
    }
  };

  const togglePlayPause = () => {
    status.playing ? activePlayer.pause() : activePlayer.play();
  };

  const skip = async (direction: 1 | -1) => {
    setPosition(0);

    const newIndex = state.currentIndex + direction;
    if (newIndex < 0 || newIndex >= state.queue.length) return;

    const track = state.queue[newIndex];
    const currentActive = state.activePlayer === 'A' ? playerA : playerB;
    const currentBuffer = state.activePlayer === 'A' ? playerB : playerA;

    // Gapless forward skip if buffer has next track ready
    const bufferReady = direction === 1 && bufferStatus.isLoaded && currentBuffer === bufferPlayer && newIndex === state.currentIndex + 1;

    if (bufferReady) {
      currentActive.pause();
      currentActive.seekTo(0);
      currentBuffer.play();

      setState({
        ...state,
        currentIndex: newIndex,
        currentTrack: track,
        activePlayer: state.activePlayer === 'A' ? 'B' : 'A',
      });

      const nextTrack = state.queue[newIndex + 1];
      if (nextTrack) loadTrack(currentActive, nextTrack.id);
      return;
    }

    // Backward skip - load on buffer player
    if (direction === -1) {
      currentActive.pause();
      currentActive.seekTo(0);
      loadTrack(currentBuffer, track.id);

      setState({
        ...state,
        currentIndex: newIndex,
        currentTrack: track,
        activePlayer: state.activePlayer === 'A' ? 'B' : 'A',
      });

      try {
        await waitForPlayerLoaded(currentBuffer);
        currentBuffer.play();

        const prevTrack = state.queue[newIndex - 1];
        if (prevTrack) loadTrack(currentActive, prevTrack.id);
      } catch (err) {
        console.error('Skip error:', err);
      }
      return;
    }

    // Forward skip without buffer - load on active player
    loadTrack(currentActive, track.id);

    setState({
      ...state,
      currentIndex: newIndex,
      currentTrack: track,
    });

    try {
      await waitForPlayerLoaded(currentActive);
      currentActive.play();

      const nextTrack = state.queue[newIndex + 1];
      if (nextTrack) loadTrack(currentBuffer, nextTrack.id);
    } catch (err) {
      console.error('Skip error:', err);
    }
  };

  const seekTo = async (newPosition: number) => {
    setPosition(newPosition);
    await activePlayer.seekTo(newPosition);
  };

  const addToQueue = (track: SimpleTrackDto) => {
    setState(prev => ({ ...prev, queue: [...prev.queue, track] }));
  };

  const addMultipleToQueue = (tracks: SimpleTrackDto[]) => {
    setState(prev => ({ ...prev, queue: [...prev.queue, ...tracks] }));
  };

  const removeFromQueue = (index: number) => {
    setState(prev => {
      const newQueue = [...prev.queue];
      newQueue.splice(index, 1);

      // Adjust currentIndex if necessary
      let newCurrentIndex = prev.currentIndex;
      if (index < prev.currentIndex) {
        newCurrentIndex = prev.currentIndex - 1;
      } else if (index === prev.currentIndex) {
        // If removing current track, don't change index (next track moves into position)
        // But if it was the last track, go to previous
        if (index >= newQueue.length && newQueue.length > 0) {
          newCurrentIndex = newQueue.length - 1;
        }
      }

      return {
        ...prev,
        queue: newQueue,
        currentIndex: Math.max(0, newCurrentIndex),
        currentTrack: newQueue[newCurrentIndex] || prev.currentTrack,
      };
    });
  };

  const playFromIndex = async (index: number) => {
    if (index < 0 || index >= state.queue.length) return;

    const track = state.queue[index];
    setPosition(0);

    activePlayer.pause();
    bufferPlayer.pause();
    activePlayer.seekTo(0);
    bufferPlayer.seekTo(0);

    setState({ ...state, currentIndex: index, currentTrack: track, activePlayer: 'A' });
    loadTrack(playerA, track.id);
    await waitForPlayerLoaded(playerA);
    playerA.play();

    const nextTrack = state.queue[index + 1];
    if (nextTrack) {
      loadTrack(playerB, nextTrack.id);
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

  const findSimilarAndAddToQueue = useCallback(async (trackId: string) => {
    try {
      const recommendations = await fetchRecommendationsForTrack({ pathParams: { trackId } });
      // Skip first track as it's the track we're getting recommendations for
      const tracksToAdd = recommendations.slice(1);
      setState(prev => ({ ...prev, queue: [...prev.queue, ...tracksToAdd] }));
      showToast(`Added ${tracksToAdd.length} similar songs to queue`);
    } catch (err) {
      console.error('Failed to fetch recommendations:', err);
      showToast('Failed to fetch recommendations');
    }
  }, [setState, showToast]);

  const reorderQueue = (fromIndex: number, toIndex: number) => {
    setState(prev => {
      const newQueue = [...prev.queue];
      const [removed] = newQueue.splice(fromIndex, 1);
      newQueue.splice(toIndex, 0, removed);

      // Adjust currentIndex if the current track was moved
      let newCurrentIndex = prev.currentIndex;
      if (fromIndex === prev.currentIndex) {
        newCurrentIndex = toIndex;
      } else if (fromIndex < prev.currentIndex && toIndex >= prev.currentIndex) {
        newCurrentIndex = prev.currentIndex - 1;
      } else if (fromIndex > prev.currentIndex && toIndex <= prev.currentIndex) {
        newCurrentIndex = prev.currentIndex + 1;
      }

      return {
        ...prev,
        queue: newQueue,
        currentIndex: newCurrentIndex,
      };
    });
  };

  // Sync position with actual player position
  useEffect(() => {
    if (!isNaN(status.currentTime)) {
      setPosition(status.currentTime);
    }
  }, [status.currentTime]);

  return {
    activeTrack: state.currentTrack,
    isPlaying: status.playing,
    progress: { position, duration: status.duration },
    queue: state.queue,
    currentIndex: state.currentIndex,
    volume: activePlayer.volume,
    isMuted,
    play,
    togglePlayPause,
    skip,
    seekTo,
    setVolume,
    toggleMute,
    addToQueue,
    addMultipleToQueue,
    removeFromQueue,
    playFromIndex,
    findSimilarAndAddToQueue,
    reorderQueue,
  };
}

// Hook that returns only player actions without subscribing to status updates
// Use this in components that only need to trigger actions, not display player state
export function usePlayerActions() {
  const { playerA, playerB } = useDualPlayers();
  const setState = useSetAtom(playerStateAtom);

  const play = async (tracks: SimpleTrackDto[], startIndex: number = 0) => {
    const track = tracks[startIndex];

    playerA.pause();
    playerB.pause();
    playerA.seekTo(0);
    playerB.seekTo(0);

    setState({ queue: tracks, currentIndex: startIndex, currentTrack: track, activePlayer: 'A' });
    loadTrack(playerA, track.id);
    await waitForPlayerLoaded(playerA);
    playerA.play();

    const nextTrack = tracks[startIndex + 1];
    if (nextTrack) {
      loadTrack(playerB, nextTrack.id);
    }
  };

  const addToQueue = (track: SimpleTrackDto) => {
    setState(prev => ({ ...prev, queue: [...prev.queue, track] }));
  };

  return {
    play,
    addToQueue,
  };
}