import { View, ScrollView, Image, Pressable, ActivityIndicator, NativeScrollEvent, NativeSyntheticEvent, Platform } from 'react-native';
import { Text } from '@/components/ui/text';
import { usePaginatedAlbums } from '@/lib/client/components';
import { baseUrl } from '@/lib/client/fetcher';
import { Link } from 'expo-router';
import { useState, useEffect } from 'react';
import type { SimpleAlbumDto } from '@/lib/client/schemas';

const ITEMS_PER_PAGE = 100;

interface AlbumCardProps {
  album: SimpleAlbumDto;
}

function AlbumCard({ album }: AlbumCardProps) {
  const isWeb = Platform.OS === 'web';
  const artworkSize = isWeb ? 150 : 180;
  const artworkPath = album.artworks?.medium ?? album.artworks?.small ?? '';
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;

  const artistNames = album.artists && album.artists.length > 4
    ? 'Various Artists'
    : album.artists?.map(a => a.name).join(', ') ?? 'Unknown Artist';

  return (
    <Link href={`/albums/${album.id}`} asChild>
      <Pressable className="p-2" style={{ width: artworkSize + 16 }}>
        <View className="gap-2" style={{ width: artworkSize }}>
          {/* Album Artwork */}
          <View className="aspect-square rounded-lg overflow-hidden bg-muted" style={{ width: artworkSize, height: artworkSize }}>
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
        <View className="flex-row flex-wrap p-2 gap-5">
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
