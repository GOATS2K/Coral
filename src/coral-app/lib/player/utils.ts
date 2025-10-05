import { Track } from 'react-native-track-player';
import { SimpleTrackDto, AlbumDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';

export interface TrackWithAlbum extends SimpleTrackDto {
  album?: AlbumDto;
}

export function convertToPlayerTrack(track: SimpleTrackDto, album?: AlbumDto): Track {
  const mainArtists = track.artists
    .filter(a => a.role === 'Main')
    .map(a => a.name)
    .join(', ') || 'Unknown Artist';

  const artworkUrl = album?.artworks?.medium
    ? `${baseUrl}${album.artworks.medium}`
    : undefined;

  return {
    id: track.id,
    url: `${baseUrl}/api/library/tracks/${track.id}/original`,
    title: track.title,
    artist: mainArtists,
    album: album?.name,
    artwork: artworkUrl,
    duration: track.durationInSeconds,
  };
}