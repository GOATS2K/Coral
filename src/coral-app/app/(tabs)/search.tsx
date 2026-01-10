import { View, ScrollView, Pressable, ActivityIndicator } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { Text } from '@/components/ui/text';
import { useState, useEffect } from 'react';
import { fetchSearch } from '@/lib/client/components';
import { TrackListing } from '@/components/track-listing';
import { ArtistCard } from '@/components/artist-card';
import { CompactAlbumCard } from '@/components/compact-album-card';
import { Music2 } from 'lucide-react-native';
import { useSetAtom } from 'jotai';
import { lastSearchQueryAtom, PlaybackSource } from '@/lib/state';
import { useInfiniteQuery } from '@tanstack/react-query';
import { Icon } from '@/components/ui/icon';
import { useDebouncedLoading } from '@/hooks/use-debounced-loading';

export default function SearchScreen() {
  const params = useLocalSearchParams();
  const setLastSearchQuery = useSetAtom(lastSearchQueryAtom);

  // Query comes from URL params (set by title bar search)
  const query = (params.q as string) || '';
  const [expandedSections, setExpandedSections] = useState({
    artists: false,
    albums: false,
  });

  const INITIAL_LIMIT = 6;
  const INITIAL_ALBUMS_LIMIT = 12;
  const EXPANDED_ARTISTS_CAP = 50;
  const EXPANDED_ALBUMS_CAP = 100;
  const ITEMS_PER_PAGE = 50;

  // Save query to atom so title bar can display it
  useEffect(() => {
    if (query) {
      setLastSearchQuery(query);
    }
  }, [query, setLastSearchQuery]);

  // Reset expanded sections when query changes
  useEffect(() => {
    setExpandedSections({ artists: false, albums: false });
  }, [query]);

  const {
    data,
    isLoading,
    error,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useInfiniteQuery({
    queryKey: ['search', query],
    queryFn: ({ pageParam }) =>
      fetchSearch({
        queryParams: {
          query: query,
          limit: ITEMS_PER_PAGE,
          offset: pageParam,
        },
      }),
    initialPageParam: 0,
    getNextPageParam: (lastPage, allPages) => {
      const totalLoaded = allPages.length * ITEMS_PER_PAGE;
      if (totalLoaded < lastPage.totalRecords) {
        return totalLoaded;
      }
      return undefined;
    },
    enabled: query.trim().length > 0,
    staleTime: 5 * 60 * 1000,
  });

  // Use debounced loading with 250ms delay
  const shouldShowLoading = useDebouncedLoading(isLoading, 250);

  // Flatten paginated data
  const artists = data?.pages.flatMap((page) => page.data.artists) ?? [];
  const albums = data?.pages.flatMap((page) => page.data.albums) ?? [];
  const tracks = data?.pages.flatMap((page) => page.data.tracks) ?? [];

  const hasResults = artists.length > 0 || albums.length > 0 || tracks.length > 0;

  // Sliced display arrays
  const displayedArtists = !expandedSections.artists
    ? artists.slice(0, INITIAL_LIMIT)
    : artists.slice(0, EXPANDED_ARTISTS_CAP);

  const displayedAlbums = !expandedSections.albums
    ? albums.slice(0, INITIAL_ALBUMS_LIMIT)
    : albums.slice(0, EXPANDED_ALBUMS_CAP);

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

  const toggleArtistsExpanded = () => {
    setExpandedSections((prev) => ({ ...prev, artists: true }));
  };

  const toggleAlbumsExpanded = () => {
    setExpandedSections((prev) => ({ ...prev, albums: true }));
  };

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 bg-background">
        {/* Results */}
        <ScrollView
          className="flex-1 px-4 pt-8"
          showsVerticalScrollIndicator={false}
          onScroll={handleScroll}
          scrollEventThrottle={16}
        >
          {shouldShowLoading && query.trim().length > 0 && (
            <View className="py-8 items-center">
              <ActivityIndicator size="large" />
            </View>
          )}

          {error && (
            <View className="py-8 items-center">
              <Text className="text-destructive">Error loading search results</Text>
            </View>
          )}

          {!isLoading && query.trim().length > 0 && !hasResults && (
            <View className="py-8 items-center">
              <Icon as={Music2} size={48} className="text-muted-foreground mb-2" />
              <Text className="text-muted-foreground">No results found</Text>
            </View>
          )}

          {!isLoading && hasResults && (
            <>
              {/* Artists */}
              {artists.length > 0 && (
                <View className="mb-6">
                  <Text variant="h4" className="mb-3">Artists</Text>
                  <View className="native:gap-2 web:flex-row web:flex-wrap web:gap-2">
                    {displayedArtists.map((artist) => (
                      <ArtistCard key={artist.id} artist={artist} />
                    ))}
                  </View>
                  {artists.length > INITIAL_LIMIT && !expandedSections.artists && (
                    <Pressable
                      onPress={toggleArtistsExpanded}
                      className="mt-3 py-2 items-center"
                    >
                      <Text className="text-primary font-medium">
                        View all {artists.length} artists
                      </Text>
                    </Pressable>
                  )}
                  {expandedSections.artists && artists.length > EXPANDED_ARTISTS_CAP && (
                    <View className="mt-3 py-2 items-center">
                      <Text className="text-muted-foreground text-sm">
                        Showing {EXPANDED_ARTISTS_CAP} of {artists.length} artists
                      </Text>
                    </View>
                  )}
                </View>
              )}

              {/* Albums */}
              {albums.length > 0 && (
                <View className="mb-6">
                  <Text variant="h4" className="mb-3">Albums</Text>
                  <View className="native:gap-2 web:flex-row web:flex-wrap web:gap-2">
                    {displayedAlbums.map((album) => (
                      <CompactAlbumCard key={album.id} album={album} />
                    ))}
                  </View>
                  {albums.length > INITIAL_ALBUMS_LIMIT && !expandedSections.albums && (
                    <Pressable
                      onPress={toggleAlbumsExpanded}
                      className="mt-3 py-2 items-center"
                    >
                      <Text className="text-primary font-medium">
                        View all {albums.length} albums
                      </Text>
                    </Pressable>
                  )}
                  {expandedSections.albums && albums.length > EXPANDED_ALBUMS_CAP && (
                    <View className="mt-3 py-2 items-center">
                      <Text className="text-muted-foreground text-sm">
                        Showing {EXPANDED_ALBUMS_CAP} of {albums.length} albums
                      </Text>
                    </View>
                  )}
                </View>
              )}

              {/* Tracks */}
              {tracks.length > 0 && (
                <View className="mb-6">
                  <Text variant="h4" className="mb-3">Tracks</Text>
                  <TrackListing
                    tracks={tracks}
                    showTrackNumber={false}
                    showCoverArt={true}
                    initializer={{ source: PlaybackSource.Search, id: query }}
                  />
                </View>
              )}
            </>
          )}

          {query.trim().length === 0 && (
            <View className="py-8 items-center">
              <Icon as={Music2} size={48} className="text-muted-foreground mb-2" />
              <Text className="text-muted-foreground">Start typing to search</Text>
            </View>
          )}

          {/* Pagination Loading Indicator */}
          {isFetchingNextPage && (
            <View className="py-4 pb-20 items-center">
              <ActivityIndicator />
            </View>
          )}

          {/* Bottom Padding */}
          {hasResults && !isFetchingNextPage && <View className="pb-20" />}
        </ScrollView>
      </View>
    </>
  );
}
