import type { AudioPlayer } from 'expo-audio';
import { baseUrl } from '@/lib/client/fetcher';

export const getTrackUrl = (trackId: string) =>
  `${baseUrl}/api/library/tracks/${trackId}/original`;

export const loadTrack = (player: AudioPlayer, trackId: string) => {
  console.log('[LOAD_TRACK] Loading track', {
    trackId,
    playerWasLoaded: player.isLoaded,
  });
  player.pause();
  player.seekTo(0);
  player.replace(getTrackUrl(trackId));
};

export const waitForPlayerLoaded = (player: AudioPlayer, timeoutMs = 5000): Promise<void> => {
  return new Promise((resolve, reject) => {
    const initialDuration = player.duration;
    const startTime = Date.now();

    const checkInterval = setInterval(() => {
      const currentDuration = player.duration;
      const durationChanged = !isNaN(currentDuration) && currentDuration !== initialDuration;

      if (player.isLoaded && durationChanged) {
        clearInterval(checkInterval);
        resolve();
      } else if (Date.now() - startTime > timeoutMs) {
        clearInterval(checkInterval);
        reject(new Error('Timeout waiting for player to load'));
      }
    }, 50);
  });
};
