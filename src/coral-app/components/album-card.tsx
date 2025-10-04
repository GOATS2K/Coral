import { View, Image, Pressable, Platform } from 'react-native';
import { Text } from '@/components/ui/text';
import { baseUrl } from '@/lib/client/fetcher';
import { Link } from 'expo-router';
import { useState, memo } from 'react';
import type { SimpleAlbumDto } from '@/lib/client/schemas';
import { PlayIcon, MoreVerticalIcon, HeartIcon, ListPlusIcon } from 'lucide-react-native';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { useAtomValue, useSetAtom } from 'jotai';
import { themeAtom, playerStateAtom } from '@/lib/state';
import { usePlayerActions } from '@/lib/player/use-player';
import { addMultipleToQueue } from '@/lib/player/player-queue-utils';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
import { useFavoriteAlbum, useRemoveFavoriteAlbum } from '@/lib/client/components';
import { useQueryClient } from '@tanstack/react-query';

interface AlbumCardProps {
  album: SimpleAlbumDto;
}

export const AlbumCard = memo(function AlbumCard({ album }: AlbumCardProps) {
  const isWeb = Platform.OS === 'web';
  const theme = useAtomValue(themeAtom);
  const { play } = usePlayerActions();
  const setState = useSetAtom(playerStateAtom);
  const queryClient = useQueryClient();
  const favoriteMutation = useFavoriteAlbum();
  const removeFavoriteMutation = useRemoveFavoriteAlbum();
  const artworkSize = isWeb ? 150 : 180;
  const artworkPath = album.artworks?.medium ?? album.artworks?.small ?? '';
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;
  const [isHovered, setIsHovered] = useState(false);

  const artistNames = album.artists && album.artists.length > 4
    ? 'Various Artists'
    : album.artists?.map(a => a.name).join(', ') ?? 'Unknown Artist';

  const fetchAlbumTracks = async () => {
    const response = await fetch(`${baseUrl}/api/library/albums/${album.id}/tracks`);
    if (!response.ok) throw new Error('Failed to fetch tracks');
    return await response.json();
  };

  const handlePlayAlbum = async (e: any) => {
    e.preventDefault();
    e.stopPropagation();

    try {
      const tracks = await fetchAlbumTracks();
      if (tracks && tracks.length > 0) {
        play(tracks, 0);
      }
    } catch (error) {
      console.error('Error playing album:', error);
    }
  };

  const handleLikeAlbum = async () => {
    try {
      if (album.favorited) {
        await removeFavoriteMutation.mutateAsync({
          pathParams: { albumId: album.id },
        });
      } else {
        await favoriteMutation.mutateAsync({
          pathParams: { albumId: album.id },
        });
      }

      // Invalidate queries to update favorited state
      await queryClient.invalidateQueries();
    } catch (error) {
      console.error('Error toggling favorite album:', error);
    }
  };

  const handleAddToQueue = async () => {
    try {
      const tracks = await fetchAlbumTracks();
      if (tracks && tracks.length > 0) {
        addMultipleToQueue(setState, tracks);
      }
    } catch (error) {
      console.error('Error adding to queue:', error);
    }
  };

  return (
    <Link href={`/albums/${album.id}`} asChild>
      <Pressable
        className="p-2"
        style={{ width: artworkSize + 16 }}
      >
        <View className="gap-2" style={{ width: artworkSize }}>
          {/* Album Artwork */}
          <View
            className="aspect-square rounded-lg overflow-hidden bg-muted relative"
            style={{ width: artworkSize, height: artworkSize }}
            onPointerEnter={isWeb ? () => setIsHovered(true) : undefined}
            onPointerLeave={isWeb ? () => setIsHovered(false) : undefined}
          >
            {artworkUrl ? (
              <Image
                source={{ uri: artworkUrl }}
                className="w-full h-full"
                resizeMode="cover"
              />
            ) : (
              <MissingAlbumCover size={64} />
            )}

            {/* Hover Overlay (Web only) */}
            {isWeb && isHovered && (
              <View className="absolute inset-0 bg-black/40 items-center justify-center">
                {/* Play Button */}
                <Pressable
                  onPress={handlePlayAlbum}
                  className="bg-white rounded-full p-3 hover:scale-110 transition-transform"
                >
                  <PlayIcon size={32} color="#000" fill="#000" />
                </Pressable>

                {/* Dropdown Menu Button */}
                <View className="absolute top-2 right-2" pointerEvents="box-none">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Pressable
                        className="bg-black/50 rounded-full p-2 hover:bg-black/70"
                        onPress={(e) => {
                          e.preventDefault();
                          e.stopPropagation();
                        }}
                      >
                        <MoreVerticalIcon size={20} color="#fff" />
                      </Pressable>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent className="w-48">
                      <DropdownMenuItem onPress={handleLikeAlbum}>
                        <HeartIcon
                          size={16}
                          color={theme === 'dark' ? '#fff' : '#000'}
                          fill={album.favorited ? (theme === 'dark' ? '#fff' : '#000') : 'none'}
                        />
                        <Text>{album.favorited ? 'Remove from favorites' : 'Like Album'}</Text>
                      </DropdownMenuItem>
                      <DropdownMenuItem onPress={handleAddToQueue}>
                        <ListPlusIcon size={16} color={theme === 'dark' ? '#fff' : '#000'} />
                        <Text>Add to Queue</Text>
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </View>
              </View>
            )}
          </View>

          {/* Album Info */}
          <View className="gap-0.5">
            <Text className="font-semibold text-sm" numberOfLines={2}>
              {album.name}
            </Text>
            <Text className="text-muted-foreground text-xs" numberOfLines={1}>
              {artistNames}
            </Text>
            {album.releaseYear && (
              <Text className="text-muted-foreground text-xs">
                {album.releaseYear}
              </Text>
            )}
          </View>
        </View>
      </Pressable>
    </Link>
  );
});
