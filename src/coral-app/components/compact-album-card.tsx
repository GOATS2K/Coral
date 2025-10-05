import { View, Pressable, Image } from 'react-native';
import { Link } from 'expo-router';
import { Text } from '@/components/ui/text';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
import { baseUrl } from '@/lib/client/fetcher';
import type { SimpleAlbumDto } from '@/lib/client/schemas';
import { memo } from 'react';

interface CompactAlbumCardProps {
  album: SimpleAlbumDto;
}

export const CompactAlbumCard = memo(function CompactAlbumCard({ album }: CompactAlbumCardProps) {
  const artworkPath = album.artworks?.small ?? '';
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;
  const artistNames = album.artists && album.artists.length > 4
    ? 'Various Artists'
    : album.artists?.map(a => a.name).join(', ') ?? 'Unknown Artist';

  return (
    <Link href={`/albums/${album.id}`} asChild>
      <Pressable className="w-full flex-row items-center gap-2.5 web:hover:bg-muted/30 active:bg-muted/50 rounded-lg p-1">
        <View className="w-12 h-12 rounded overflow-hidden flex-shrink-0">
          {artworkUrl ? (
            <Image
              source={{ uri: artworkUrl }}
              className="w-full h-full"
              resizeMode="cover"
            />
          ) : (
            <MissingAlbumCover size={20} />
          )}
        </View>
        <View className="flex-1 min-w-0">
          <Text className="font-medium text-sm" numberOfLines={1}>{album.name}</Text>
          <Text className="text-muted-foreground text-xs" numberOfLines={1}>{artistNames}</Text>
          {album.releaseYear && (
            <Text className="text-muted-foreground text-xs">{album.releaseYear}</Text>
          )}
        </View>
      </Pressable>
    </Link>
  );
});
