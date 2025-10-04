import { View, Pressable, ActivityIndicator, Platform, FlatList } from 'react-native';
import { Text } from '@/components/ui/text';
import { fetchPaginatedAlbums } from '@/lib/client/components';
import { useInfiniteQuery } from '@tanstack/react-query';
import { AlbumCard } from '@/components/album-card';

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
