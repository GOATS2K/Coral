import { View, Pressable, ActivityIndicator, ScrollView } from 'react-native';
import { Text } from '@/components/ui/text';
import { fetchPaginatedAlbums } from '@/lib/client/components';
import { useInfiniteQuery } from '@tanstack/react-query';
import { UniversalAlbumCard } from '@/components/universal-album-card';
import { DebouncedLoader } from '@/components/debounced-loader';

const ITEMS_PER_PAGE = 100;

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

  const handleScroll = (event: any) => {
    const { layoutMeasurement, contentOffset, contentSize } = event.nativeEvent;
    const paddingToBottom = 200;
    const isCloseToBottom =
      layoutMeasurement.height + contentOffset.y >=
      contentSize.height - paddingToBottom;

    if (isCloseToBottom && hasNextPage && !isFetchingNextPage) {
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
      <DebouncedLoader isLoading={isLoading}>
        <ActivityIndicator size="large" />
      </DebouncedLoader>
    );
  }

  if (albums.length === 0) {
    return (
      <View className="flex-1 items-center justify-center bg-background p-4">
        <Text className="text-muted-foreground">No albums found</Text>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-background">
      <ScrollView
        className="flex-1"
        showsVerticalScrollIndicator={false}
        onScroll={handleScroll}
        scrollEventThrottle={400}
      >
        <View className="flex-row flex-wrap px-2 py-2">
          {albums.map((album) => (
            <UniversalAlbumCard key={album.id} album={album} />
          ))}
        </View>
        {isFetchingNextPage && (
          <View className="py-4 items-center">
            <ActivityIndicator />
          </View>
        )}
        <View className="pb-20" />
      </ScrollView>
    </View>
  );
}
