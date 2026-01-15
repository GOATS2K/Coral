import type { SimpleTrackDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';

export function formatTime(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const mins = Math.floor((seconds % 3600) / 60);
  const secs = Math.floor(seconds % 60);

  if (hours >= 1) {
    return `${hours}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }

  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

export function getArtistNames(track: SimpleTrackDto): string {
  return track.artists
    ?.filter(a => a.role === 'Main')
    .map(a => a.name)
    .join(', ') || 'Unknown Artist';
}

export function getArtworkUrl(albumId: string | undefined, size: 'small' | 'large' = 'small'): string | null {
  return albumId ? `${baseUrl}/api/artwork?albumId=${albumId}&size=${size}` : null;
}
