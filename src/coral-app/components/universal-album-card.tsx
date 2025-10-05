import { Platform } from 'react-native';
import { AlbumCard } from './album-card';
import { CompactAlbumCard } from './compact-album-card';
import type { SimpleAlbumDto } from '@/lib/client/schemas';

interface UniversalAlbumCardProps {
  album: SimpleAlbumDto;
}

export function UniversalAlbumCard({ album }: UniversalAlbumCardProps) {
  return Platform.OS === 'web' ? (
    <AlbumCard album={album} />
  ) : (
    <CompactAlbumCard album={album} />
  );
}
