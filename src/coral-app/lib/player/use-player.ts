import { useAudioPlayerStatus } from 'expo-audio';
import { useAtom } from 'jotai';
import { useEffect } from 'react';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';
import { playerStateAtom } from '@/lib/state';
import { useAudioPlayerInstance } from './player-provider';

export function usePlayer() {
  const player = useAudioPlayerInstance();
  const status = useAudioPlayerStatus(player);
  const [state, setState] = useAtom(playerStateAtom);

  const getTrackUrl = (trackId: string) => `${baseUrl}/api/library/tracks/${trackId}/original`;

  const play = async (tracks: SimpleTrackDto[], startIndex: number = 0) => {
    const track = tracks[startIndex];
    await player.pause();
    await player.seekTo(0);
    setState({ queue: tracks, currentIndex: startIndex, currentTrack: track });
    await player.replace(getTrackUrl(track.id));
    await player.play();
  };

  const togglePlayPause = () => {
    status.playing ? player.pause() : player.play();
  };

  const skip = async (direction: 1 | -1) => {
    const newIndex = state.currentIndex + direction;
    if (newIndex >= 0 && newIndex < state.queue.length) {
      const track = state.queue[newIndex];
      await player.pause();
      await player.seekTo(0);
      setState(prev => ({ ...prev, currentIndex: newIndex, currentTrack: track }));
      await player.replace(getTrackUrl(track.id));
      await player.play();
    }
  };

  const seekTo = (position: number) => {
    player.seekTo(position);
  };

  const addToQueue = (track: SimpleTrackDto) => {
    setState(prev => ({ ...prev, queue: [...prev.queue, track] }));
  };

  // Auto-skip to next track when current track finishes
  useEffect(() => {
    if (status.didJustFinish && state.currentIndex < state.queue.length - 1) {
      skip(1);
    }
  }, [status.didJustFinish]);

  return {
    activeTrack: state.currentTrack,
    isPlaying: status.playing,
    progress: { position: status.currentTime, duration: status.duration },
    queue: state.queue,
    play,
    togglePlayPause,
    skip,
    seekTo: (position: number) => player.seekTo(position),
    addToQueue,
  };
}