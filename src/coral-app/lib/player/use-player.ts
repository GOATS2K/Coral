import { useAudioPlayerStatus } from 'expo-audio';
import { useAtom } from 'jotai';
import { useEffect, useState } from 'react';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { playerStateAtom } from '@/lib/state';
import { useDualPlayers } from './player-provider';
import { getTrackUrl, loadTrack, waitForPlayerLoaded } from './player-utils';

export function usePlayer() {
  const { playerA, playerB } = useDualPlayers();
  const [state, setState] = useAtom(playerStateAtom);
  const [position, setPosition] = useState(0);

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

    setState(prev => {
      const newIndex = prev.currentIndex + direction;

      if (newIndex < 0 || newIndex >= prev.queue.length) return prev;

      const track = prev.queue[newIndex];
      const currentActive = prev.activePlayer === 'A' ? playerA : playerB;
      const currentBuffer = prev.activePlayer === 'A' ? playerB : playerA;

      // Gapless forward skip if buffer has next track ready
      const bufferReady = direction === 1 && bufferStatus.isLoaded && currentBuffer === bufferPlayer && newIndex === prev.currentIndex + 1;

      if (bufferReady) {
        currentActive.pause();
        currentActive.seekTo(0);
        currentBuffer.play();

        const nextTrack = prev.queue[newIndex + 1];
        if (nextTrack) {
          loadTrack(currentActive, nextTrack.id);
        }

        return {
          ...prev,
          currentIndex: newIndex,
          currentTrack: track,
          activePlayer: prev.activePlayer === 'A' ? 'B' : 'A',
        };
      }

      // Backward skip or forward without buffer
      const useBuffer = direction === -1;
      const targetPlayer = useBuffer ? currentBuffer : currentActive;

      if (useBuffer) {
        currentActive.pause();
        currentActive.seekTo(0);

        loadTrack(targetPlayer, track.id);
        waitForPlayerLoaded(targetPlayer)
          .then(() => {
            targetPlayer.play();
            const adjacentIndex = newIndex + (direction === -1 ? -1 : 1);
            const adjacentTrack = prev.queue[adjacentIndex];
            if (adjacentTrack && adjacentIndex >= 0) {
              loadTrack(currentActive, adjacentTrack.id);
            }
          })
          .catch(err => console.error('Skip error:', err));

        return {
          ...prev,
          currentIndex: newIndex,
          currentTrack: track,
          activePlayer: prev.activePlayer === 'A' ? 'B' : 'A',
        };
      } else {
        loadTrack(targetPlayer, track.id);
        waitForPlayerLoaded(targetPlayer)
          .then(() => {
            targetPlayer.play();
            const nextTrack = prev.queue[newIndex + 1];
            if (nextTrack) {
              loadTrack(currentBuffer, nextTrack.id);
            }
          })
          .catch(err => console.error('Skip error:', err));

        return {
          ...prev,
          currentIndex: newIndex,
          currentTrack: track,
        };
      }
    });
  };

  const seekTo = async (newPosition: number) => {
    setPosition(newPosition);
    await activePlayer.seekTo(newPosition);
  };

  const addToQueue = (track: SimpleTrackDto) => {
    setState(prev => ({ ...prev, queue: [...prev.queue, track] }));
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
    play,
    togglePlayPause,
    skip,
    seekTo,
    addToQueue,
  };
}