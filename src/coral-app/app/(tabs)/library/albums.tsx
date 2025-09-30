import { View, ScrollView, Image, Pressable, ActivityIndicator, NativeScrollEvent, NativeSyntheticEvent, Platform } from 'react-native';
import { Text } from '@/components/ui/text';
import { usePaginatedAlbums, useAlbumTracks } from '@/lib/client/components';
import { baseUrl } from '@/lib/client/fetcher';
import { Link } from 'expo-router';
import { useState, useEffect } from 'react';
import type { SimpleAlbumDto } from '@/lib/client/schemas';
import { PlayIcon, MoreVerticalIcon, HeartIcon, ListPlusIcon } from 'lucide-react-native';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';
import { usePlayer } from '@/lib/player/use-player';

const ITEMS_PER_PAGE = 100;

interface AlbumCardProps {
  album: SimpleAlbumDto;
}

function AlbumCard({ album }: AlbumCardProps) {
  const isWeb = Platform.OS === 'web';
  const theme = useAtomValue(themeAtom);
  const { play, addToQueue } = usePlayer();
  const artworkSize = isWeb ? 150 : 180;
  const artworkPath = album.artworks?.medium ?? album.artworks?.small ?? '';
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;
  const [isHovered, setIsHovered] = useState(false);

  const { data: tracks, refetch } = useAlbumTracks(
    { pathParams: { albumId: album.id } },
    { enabled: false }
  );

  const artistNames = album.artists && album.artists.length > 4
    ? 'Various Artists'
    : album.artists?.map(a => a.name).join(', ') ?? 'Unknown Artist';

  const handlePlayAlbum = async (e: any) => {
    e.preventDefault();
    e.stopPropagation();

    const result = tracks || (await refetch()).data;
    if (result && result.length > 0) {
      play(result, 0);
    }
  };

  const handleLikeAlbum = () => {
    // TODO: Implement like album functionality
    console.log('Like album:', album.id);
  };

  const handleAddToQueue = async () => {
    const result = tracks || (await refetch()).data;
    if (result && result.length > 0) {
      result.forEach((track) => addToQueue(track));
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
}

export default function AlbumsScreen() {
  const [offset, setOffset] = useState(0);
  const [allAlbums, setAllAlbums] = useState<SimpleAlbumDto[]>([]);
  const [isLoadingMore, setIsLoadingMore] = useState(false);

  const { data, error, isLoading, refetch } = usePaginatedAlbums({
    queryParams: {
      limit: ITEMS_PER_PAGE,
      offset,
    },
  });

  // Append new data when it arrives
  useEffect(() => {
    if (data?.data && data.data.length > 0) {
      setAllAlbums(prev => {
        // Check if this data is already in our list
        const firstNewId = data.data![0].id;
        if (prev.find(a => a.id === firstNewId)) {
          return prev;
        }
        return [...prev, ...data.data!];
      });
      setIsLoadingMore(false);
    }
  }, [data]);

  const hasMore = data ? (data.availableRecords ?? 0) > 0 : false;

  const handleScroll = (event: NativeSyntheticEvent<NativeScrollEvent>) => {
    const { layoutMeasurement, contentOffset, contentSize } = event.nativeEvent;
    const paddingToBottom = 1500;
    const isCloseToBottom = layoutMeasurement.height + contentOffset.y >= contentSize.height - paddingToBottom;

    if (isCloseToBottom && !isLoading && !isLoadingMore && hasMore) {
      setIsLoadingMore(true);
      setOffset(prev => prev + ITEMS_PER_PAGE);
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

  if (isLoading && allAlbums.length === 0) {
    return (
      <View className="flex-1 items-center justify-center bg-background">
        <ActivityIndicator size="large" />
      </View>
    );
  }

  if (allAlbums.length === 0) {
    return (
      <View className="flex-1 items-center justify-center bg-background p-4">
        <Text className="text-muted-foreground">No albums found</Text>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-background">
      <ScrollView
        onScroll={handleScroll}
        scrollEventThrottle={400}
        className="flex-1"
      >
        <View className="flex-row flex-wrap p-2">
          {allAlbums.map((album) => (
            <AlbumCard key={album.id} album={album} />
          ))}
        </View>

        {(isLoading || isLoadingMore) && hasMore && (
          <View className="py-4">
            <ActivityIndicator />
          </View>
        )}
      </ScrollView>
    </View>
  );
}
