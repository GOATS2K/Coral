import type { AudioPlayer } from 'expo-audio';
import { fetchGetOriginalStreamUrl } from '@/lib/client/components';

export const getTrackUrl = async (trackId: string): Promise<string> => {
  // Fetch the signed URL from the API
  const streamData = await fetchGetOriginalStreamUrl({ pathParams: { trackId } });
  return streamData.link;
};

export const loadTrack = async (player: AudioPlayer, trackId: string) => {
  // Don't interrupt a currently playing track - let it finish naturally
  // Only pause/seek if the player is NOT playing
  if (!player.playing) {
    player.pause();
    player.seekTo(0);
  }

  const url = await getTrackUrl(trackId);
  player.replace(url);
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
