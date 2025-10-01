import { View, Image, Pressable, ActivityIndicator, Platform } from 'react-native';
import { Text } from '@/components/ui/text';
import { fetchPaginatedAlbums } from '@/lib/client/components';
import { baseUrl } from '@/lib/client/fetcher';
import { Link, useFocusEffect } from 'expo-router';
import { useState, useRef, useCallback, useEffect } from 'react';
import type { SimpleAlbumDto } from '@/lib/client/schemas';
import { PlayIcon, MoreVerticalIcon, HeartIcon, ListPlusIcon } from 'lucide-react-native';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { useAtomValue, useAtom } from 'jotai';
import { themeAtom, albumsScrollStateAtom } from '@/lib/state';
import { usePlayer } from '@/lib/player/use-player';
import { LegendList } from '@legendapp/list';
import { useInfiniteQuery } from '@tanstack/react-query';

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
}

export default function AlbumsScreen() {
  // Track if screen is focused to prevent background loading
  const [isFocused, setIsFocused] = useState(false);

  // Scroll state for position restoration
  const [scrollState, setScrollState] = useAtom(albumsScrollStateAtom);
  const listRef = useRef<LegendList<SimpleAlbumDto>>(null);

  // Prevent onEndReached from firing on mount
  // See: https://stackoverflow.com/questions/47910127/flatlist-calls-onendreached-when-its-rendered
  const onEndReachedCalledDuringMomentum = useRef(true);

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
    refetchOnMount: false,
    refetchOnWindowFocus: false,
    staleTime: 5 * 60 * 1000,
  });

  // Flatten all pages into a single array
  const albums = data?.pages.flatMap((page) => page.data) ?? [];

  // Manage focus state - allow loading only when screen is focused
  useFocusEffect(
    useCallback(() => {
      setIsFocused(true);
      onEndReachedCalledDuringMomentum.current = true;

      return () => {
        setIsFocused(false);
        // Save current index and mark that we need restoration when we come back
        setScrollState(prev => ({
          ...prev,
          needsRestoration: true,
          savedFirstVisibleIndex: prev.firstVisibleIndex
        }));
      };
    }, [setScrollState])
  );

  // Load pages until we reach saved page count (for scroll restoration)
  useEffect(() => {
    const currentPages = data?.pages.length ?? 0;

    if (currentPages < scrollState.savedPageCount && hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [data?.pages.length, scrollState.savedPageCount, hasNextPage, isFetchingNextPage, fetchNextPage]);

  // Track first visible item for index-based scroll restoration
  const handleViewableItemsChanged = useCallback(({ viewableItems }: any) => {
    if (viewableItems && viewableItems.length > 0) {
      const firstVisible = viewableItems[0];
      const firstIndex = firstVisible.index ?? 0;

      // Update state with first visible index
      setScrollState(prev => ({
        ...prev,
        firstVisibleIndex: firstIndex
      }));
    }
  }, [setScrollState]);

  // Restore scroll position once all pages are loaded
  useEffect(() => {
    const currentPages = data?.pages.length ?? 0;

    // Calculate minimum pages needed to contain the saved index
    const requiredPages = Math.ceil((scrollState.savedFirstVisibleIndex + 1) / ITEMS_PER_PAGE);
    const minPages = Math.max(scrollState.savedPageCount, requiredPages);

    // Load more pages if we don't have enough for the saved index
    if (currentPages < minPages && hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
      return;
    }

    if (isFocused &&
        scrollState.needsRestoration &&
        currentPages >= minPages &&
        scrollState.savedFirstVisibleIndex >= 0 &&
        albums.length > scrollState.savedFirstVisibleIndex) {
      // Clear flag first to prevent re-triggering
      setScrollState(prev => ({ ...prev, needsRestoration: false }));

      // Use scrollToIndex for reliable item-based restoration
      requestAnimationFrame(() => {
        listRef.current?.scrollToIndex({
          index: scrollState.savedFirstVisibleIndex,
          animated: false
        });
      });
    }
  }, [isFocused, data?.pages.length, scrollState, setScrollState, albums.length, hasNextPage, isFetchingNextPage, fetchNextPage]);

  const loadMore = () => {
    if (!isFocused) {
      return;
    }

    if (!onEndReachedCalledDuringMomentum.current && hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
      onEndReachedCalledDuringMomentum.current = true;
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
      <LegendList
        ref={listRef}
        data={albums}
        renderItem={({ item }) => <AlbumCard album={item} />}
        keyExtractor={(item) => item.id}
        numColumns={numColumns}
        estimatedItemSize={isWeb ? 166 : 196}
        onEndReached={loadMore}
        onEndReachedThreshold={0.5}
        onScroll={(event) => {
          const offset = event.nativeEvent.contentOffset.y;
          const currentPages = data?.pages.length ?? 1;

          // Only update index when focused to avoid pollution during navigation
          if (isFocused) {
            // Calculate approximate first visible index from scroll position
            // For grid layouts: each row contains numColumns items
            const estimatedRowHeight = isWeb ? 166 : 196;
            const approximateRow = Math.floor(offset / estimatedRowHeight);
            const approximateIndex = approximateRow * numColumns;

            // If index exceeds loaded items, load next page
            if (approximateIndex >= albums.length && hasNextPage && !isFetchingNextPage) {
              fetchNextPage();
            }

            // Save scroll position, page count, and approximate index (unclamped)
            setScrollState(prev => ({
              ...prev,
              scrollPosition: offset,
              savedPageCount: currentPages,
              firstVisibleIndex: Math.max(0, approximateIndex)
            }));
          }

          // Fallback for web - onMomentumScrollBegin doesn't fire with mouse wheel
          if (onEndReachedCalledDuringMomentum.current) {
            onEndReachedCalledDuringMomentum.current = false;
          }
        }}
        onMomentumScrollBegin={() => {
          onEndReachedCalledDuringMomentum.current = false;
        }}
        onViewableItemsChanged={handleViewableItemsChanged}
        contentContainerStyle={{ padding: 8 }}
        ListFooterComponent={
          isFetchingNextPage ? (
            <View className="py-4">
              <ActivityIndicator />
            </View>
          ) : null
        }
        recycleItems
      />
    </View>
  );
}
