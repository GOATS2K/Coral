import { View, ScrollView, Pressable, ActivityIndicator, Platform } from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { Text } from '@/components/ui/text';
import { Input } from '@/components/ui/input';
import { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { fetchSearch } from '@/lib/client/components';
import { TrackListing } from '@/components/track-listing';
import { ArtistCard } from '@/components/artist-card';
import { CompactAlbumCard } from '@/components/compact-album-card';
import { Music2 } from 'lucide-react-native';
import { useAtom, useAtomValue } from 'jotai';
import { lastSearchQueryAtom, PlaybackSource, themeAtom } from '@/lib/state';
import { useInfiniteQuery } from '@tanstack/react-query';
import { Icon } from '@/components/ui/icon';

export default function SearchScreen() {
  const theme = useAtomValue(themeAtom);
  const params = useLocalSearchParams();
  const router = useRouter();
  const [lastSearchQuery, setLastSearchQuery] = useAtom(lastSearchQueryAtom);
  const isUpdatingUrlRef = useRef(false);

  // Initialize from URL param, fallback to atom
  const initialQuery = (params.q as string) || lastSearchQuery;
  const [query, setQuery] = useState(initialQuery);
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery);
  const [expandedSections, setExpandedSections] = useState({
    artists: false,
    albums: false,
  });
  const [showLoading, setShowLoading] = useState(false);

  const INITIAL_LIMIT = 6;
  const INITIAL_ALBUMS_LIMIT = 12;
  const EXPANDED_ARTISTS_CAP = 50;
  const EXPANDED_ALBUMS_CAP = 100;
  const ITEMS_PER_PAGE = 50;

  // Sync URL param changes to local state (browser back/forward)
  useEffect(() => {
    if (isUpdatingUrlRef.current) {
      isUpdatingUrlRef.current = false;
      return;
    }
    const urlQuery = params.q as string || '';
    setQuery(urlQuery);
    setDebouncedQuery(urlQuery);
  }, [params.q]);

  // Debounce search query
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(query);
    }, 300);
    return () => clearTimeout(timer);
  }, [query]);

  // Save debounced query to atom and URL parameter
  useEffect(() => {
    if (debouncedQuery) {
      setLastSearchQuery(debouncedQuery);
      // Only update URL if it's different from current param
      if (params.q !== debouncedQuery) {
        isUpdatingUrlRef.current = true;
        router.setParams({ q: debouncedQuery });
      }
    } else {
      if (params.q) {
        isUpdatingUrlRef.current = true;
        router.setParams({ q: undefined });
      }
    }
  }, [debouncedQuery, setLastSearchQuery, router, params.q]);

  // Reset expanded sections when query changes
  useEffect(() => {
    setExpandedSections({ artists: false, albums: false });
  }, [debouncedQuery]);

  const {
    data,
    isLoading,
    error,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useInfiniteQuery({
    queryKey: ['search', debouncedQuery],
    queryFn: ({ pageParam }) =>
      fetchSearch({
        queryParams: {
          query: debouncedQuery,
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
    enabled: debouncedQuery.trim().length > 0,
    staleTime: 5 * 60 * 1000,
  });

  // Delayed loading indicator
  useEffect(() => {
    if (isLoading) {
      const timer = setTimeout(() => {
        setShowLoading(true);
      }, 1000);
      return () => clearTimeout(timer);
    } else {
      setShowLoading(false);
    }
  }, [isLoading]);

  // Flatten paginated data - memoized to prevent re-flattening on every render
  const artists = useMemo(() =>
    data?.pages.flatMap((page) => page.data.artists) ?? [],
    [data?.pages]
  );
  const albums = useMemo(() =>
    data?.pages.flatMap((page) => page.data.albums) ?? [],
    [data?.pages]
  );
  const tracks = useMemo(() =>
    data?.pages.flatMap((page) => page.data.tracks) ?? [],
    [data?.pages]
  );

  const hasResults = artists.length > 0 || albums.length > 0 || tracks.length > 0;

  // Sliced display arrays - memoized to prevent re-slicing on every render
  const displayedArtists = useMemo(() => {
    if (!expandedSections.artists) {
      return artists.slice(0, INITIAL_LIMIT);
    }
    return artists.slice(0, EXPANDED_ARTISTS_CAP);
  }, [artists, expandedSections.artists]);

  const displayedAlbums = useMemo(() => {
    if (!expandedSections.albums) {
      return albums.slice(0, INITIAL_ALBUMS_LIMIT);
    }
    return albums.slice(0, EXPANDED_ALBUMS_CAP);
  }, [albums, expandedSections.albums]);

  const handleScroll = useCallback((event: any) => {
    const { layoutMeasurement, contentOffset, contentSize } = event.nativeEvent;
    const paddingToBottom = 200;
    const isCloseToBottom =
      layoutMeasurement.height + contentOffset.y >=
      contentSize.height - paddingToBottom;

    if (isCloseToBottom && hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const toggleArtistsExpanded = useCallback(() => {
    setExpandedSections((prev) => ({ ...prev, artists: true }));
  }, []);

  const toggleAlbumsExpanded = useCallback(() => {
    setExpandedSections((prev) => ({ ...prev, albums: true }));
  }, []);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 bg-background">
        {/* Search Input */}
        <View className="px-4 pt-4 pb-2">
          <Input
            placeholder="Search for artists, albums, or tracks..."
            value={query}
            onChangeText={setQuery}
            className="text-base"
            autoFocus={Platform.OS === 'web'}
          />
        </View>

        {/* Results */}
        <ScrollView
          className="flex-1 px-4"
          showsVerticalScrollIndicator={false}
          onScroll={handleScroll}
          scrollEventThrottle={16}
        >
          {showLoading && debouncedQuery.trim().length > 0 && (
            <View className="py-8 items-center">
              <ActivityIndicator size="large" />
            </View>
          )}

          {error && (
            <View className="py-8 items-center">
              <Text className="text-destructive">Error loading search results</Text>
            </View>
          )}

          {!isLoading && debouncedQuery.trim().length > 0 && !hasResults && (
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
                    initializer={{ source: PlaybackSource.Search, id: debouncedQuery }}
                  />
                </View>
              )}
            </>
          )}

          {debouncedQuery.trim().length === 0 && (
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
