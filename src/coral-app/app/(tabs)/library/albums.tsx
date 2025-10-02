import { View, Image, Pressable, ActivityIndicator, Platform, FlatList } from 'react-native';
import { Text } from '@/components/ui/text';
import { fetchPaginatedAlbums } from '@/lib/client/components';
import { baseUrl } from '@/lib/client/fetcher';
import { Link } from 'expo-router';
import { useState, memo } from 'react';
import type { SimpleAlbumDto } from '@/lib/client/schemas';
import { PlayIcon, MoreVerticalIcon, HeartIcon, ListPlusIcon } from 'lucide-react-native';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';
import { usePlayerActions } from '@/lib/player/use-player';
import { useInfiniteQuery } from '@tanstack/react-query';

const ITEMS_PER_PAGE = 100;

interface AlbumCardProps {
  album: SimpleAlbumDto;
}

const AlbumCard = memo(function AlbumCard({ album }: AlbumCardProps) {
  const isWeb = Platform.OS === 'web';
  const theme = useAtomValue(themeAtom);
  const { play, addToQueue } = usePlayerActions();
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

  const handleLikeAlbum = () => {
    console.log('Like album:', album.id);
  };

  const handleAddToQueue = async () => {
    try {
      const tracks = await fetchAlbumTracks();
      if (tracks && tracks.length > 0) {
        tracks.forEach((track) => addToQueue(track));
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
              <View className="w-full h-full items-center justify-center">
                <Text className="text-muted-foreground">No Cover</Text>
              </View>
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
                        <HeartIcon size={16} color={theme === 'dark' ? '#fff' : '#000'} />
                        <Text>Like Album</Text>
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

export default function AlbumsScreen() {
  const {
    data,
    error,
    isLoading,
    isFetchingNextPage,
    hasNextPage,
    fetchNextPage,
    refetch,
  } = useInfiniteQuery({
    queryKey: ['albums', 'paginated'],
    queryFn: ({ pageParam }) =>
      fetchPaginatedAlbums({
        queryParams: {
          limit: ITEMS_PER_PAGE,
          offset: pageParam,
        },
      }),
    initialPageParam: 0,
    getNextPageParam: (lastPage, allPages) => {
      if (lastPage.availableRecords > 0) {
        return allPages.length * ITEMS_PER_PAGE;
      }
      return undefined;
    },
    staleTime: 5 * 60 * 1000,
  });

  const albums = data?.pages.flatMap((page) => page.data) ?? [];

  const loadMore = () => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  };

  if (error) {
    return (
      <View className="flex-1 items-center justify-center bg-background p-4">
        <Text className="text-destructive mb-4">Error loading albums</Text>
        <Pressable onPress={() => refetch()} className="px-4 py-2 bg-accent rounded-lg">
          <Text>Retry</Text>
        </Pressable>
      </View>
    );
  }

  if (isLoading) {
    return (
      <View className="flex-1 items-center justify-center bg-background">
        <ActivityIndicator size="large" />
      </View>
    );
  }

  if (albums.length === 0) {
    return (
      <View className="flex-1 items-center justify-center bg-background p-4">
        <Text className="text-muted-foreground">No albums found</Text>
      </View>
    );
  }

  const isWeb = Platform.OS === 'web';
  const numColumns = isWeb ? 6 : 2;

  return (
    <View className="flex-1 bg-background">
      <FlatList
        data={albums}
        renderItem={({ item }) => <AlbumCard album={item} />}
        keyExtractor={(item) => item.id}
        numColumns={numColumns}
        onEndReached={loadMore}
        onEndReachedThreshold={0.5}
        contentContainerStyle={{ padding: 8 }}
        ListFooterComponent={
          isFetchingNextPage ? (
            <View className="py-4">
              <ActivityIndicator />
            </View>
          ) : null
        }
      />
    </View>
  );
}
