import { useEffect, useRef } from 'react';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { getArtistNames, getArtworkUrl } from './player-format-utils';

interface UseMediaSessionProps {
  activeTrack: SimpleTrackDto | null;
  isPlaying: boolean;
  progress: { position: number; duration: number };
  togglePlayPause: () => void;
  skip: (direction: 1 | -1) => void;
  seekTo: (position: number) => void;
}

export function useMediaSession({ activeTrack, isPlaying, progress, togglePlayPause, skip, seekTo }: UseMediaSessionProps) {
  // Create refs for handler functions
  const togglePlayPauseRef = useRef(togglePlayPause);
  const skipRef = useRef(skip);
  const seekToRef = useRef(seekTo);

  // Update refs when functions change (happens on every render, but that's OK - refs don't trigger re-renders)
  togglePlayPauseRef.current = togglePlayPause;
  skipRef.current = skip;
  seekToRef.current = seekTo;

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

  // Register handlers ONCE with stable wrapper functions that read from refs
  useEffect(() => {
    if (!('mediaSession' in navigator)) return;

    console.log('🎵 [MediaSession] Registering handlers (once)', new Date().toISOString());

    navigator.mediaSession.setActionHandler('play', () => togglePlayPauseRef.current());
    navigator.mediaSession.setActionHandler('pause', () => togglePlayPauseRef.current());
    navigator.mediaSession.setActionHandler('previoustrack', () => skipRef.current(-1));
    navigator.mediaSession.setActionHandler('nexttrack', () => skipRef.current(1));
    navigator.mediaSession.setActionHandler('seekto', (details) => {
      if (details.seekTime !== undefined) seekToRef.current(details.seekTime);
    });

    return () => {
      console.log('🛑 [MediaSession] Cleanup (once)', new Date().toISOString());
      if ('mediaSession' in navigator) {
        navigator.mediaSession.setActionHandler('play', null);
        navigator.mediaSession.setActionHandler('pause', null);
        navigator.mediaSession.setActionHandler('previoustrack', null);
        navigator.mediaSession.setActionHandler('nexttrack', null);
        navigator.mediaSession.setActionHandler('seekto', null);
      }
    };
  }, []); // Empty deps - run once on mount, cleanup once on unmount

  // Update playback state when playing/paused changes
  useEffect(() => {
    if (!('mediaSession' in navigator)) return;

    // Sync dummy audio element if it exists (used by mpv for MediaSession activation)
    // The dummy audio must be synced with playback state for Windows SMTC to work correctly
    const dummyAudio = document.querySelector('audio[data-mpv-dummy="true"]') as HTMLAudioElement;
    if (dummyAudio) {
      if (isPlaying) {
        dummyAudio.play().catch(() => {
          // Ignore playback errors (autoplay policy, etc.)
        });
      } else {
        dummyAudio.pause();
      }
    }

    navigator.mediaSession.playbackState = isPlaying ? 'playing' : 'paused';
  }, [isPlaying]);

  // Update position state when track or position changes
  useEffect(() => {
    if (!('mediaSession' in navigator) || !activeTrack) return;

    // Check if setPositionState is supported (not all browsers support it)
    if (typeof navigator.mediaSession.setPositionState !== 'function') {
      return;
    }

    const duration = progress.duration || activeTrack.durationInSeconds || 0;

    // Only update if we have valid duration
    if (duration > 0) {
      try {
        navigator.mediaSession.setPositionState({
          duration,
          position: Math.min(progress.position, duration),
          playbackRate: 1.0,
        });
      } catch (error) {
        // Some browsers may throw errors with certain values
        console.warn('[MediaSession] Failed to set position state:', error);
      }
    }
  }, [activeTrack, progress.position, progress.duration]);
}
