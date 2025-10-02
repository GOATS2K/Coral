import { useEffect } from 'react';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { getArtistNames, getArtworkUrl } from './player-format-utils';

interface UseMediaSessionProps {
  activeTrack: SimpleTrackDto | null;
  togglePlayPause: () => void;
  skip: (direction: 1 | -1) => void;
  seekTo: (position: number) => void;
}

export function useMediaSession({ activeTrack, togglePlayPause, skip, seekTo }: UseMediaSessionProps) {
  // Update metadata when track changes
  useEffect(() => {
    if (!('mediaSession' in navigator) || !activeTrack) return;

    const artworkUrl = getArtworkUrl(activeTrack.album?.id);
    const artistNames = getArtistNames(activeTrack);

    navigator.mediaSession.metadata = new MediaMetadata({
      title: activeTrack.title,
      artist: artistNames,
      album: activeTrack.album?.name || '',
      artwork: artworkUrl ? [
        { src: artworkUrl, sizes: '300x300', type: 'image/jpeg' }
      ] : [],
    });
  }, [activeTrack]);

  // Register handlers once
  useEffect(() => {
    if (!('mediaSession' in navigator)) return;

    navigator.mediaSession.setActionHandler('play', () => togglePlayPause());
    navigator.mediaSession.setActionHandler('pause', () => togglePlayPause());
    navigator.mediaSession.setActionHandler('previoustrack', () => skip(-1));
    navigator.mediaSession.setActionHandler('nexttrack', () => skip(1));
    navigator.mediaSession.setActionHandler('seekto', (details) => {
      if (details.seekTime !== undefined) seekTo(details.seekTime);
    });

    return () => {
      if ('mediaSession' in navigator) {
        navigator.mediaSession.setActionHandler('play', null);
        navigator.mediaSession.setActionHandler('pause', null);
        navigator.mediaSession.setActionHandler('previoustrack', null);
        navigator.mediaSession.setActionHandler('nexttrack', null);
        navigator.mediaSession.setActionHandler('seekto', null);
      }
    };
  }, [togglePlayPause, skip, seekTo]);
}
