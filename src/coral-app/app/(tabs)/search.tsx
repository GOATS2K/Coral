import { View, ScrollView, Pressable, Image, ActivityIndicator, Platform } from 'react-native';
import { Stack, Link, useLocalSearchParams, useRouter } from 'expo-router';
import { Text } from '@/components/ui/text';
import { Input } from '@/components/ui/input';
import { useState, useEffect, useRef } from 'react';
import { fetchSearch } from '@/lib/client/components';
import { TrackListing } from '@/components/track-listing';
import { ArtistCard } from '@/components/artist-card';
import { baseUrl } from '@/lib/client/fetcher';
import { Music2 } from 'lucide-react-native';
import { useAtom, useAtomValue } from 'jotai';
import { lastSearchQueryAtom, PlaybackSource, themeAtom } from '@/lib/state';
import { useInfiniteQuery } from '@tanstack/react-query';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';

export default function SearchScreen() {
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
  const theme = useAtomValue(themeAtom);

  const INITIAL_LIMIT = 5;
  const INITIAL_ALBUMS_LIMIT = 9;
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

  // Flatten paginated data
  const artists = data?.pages.flatMap((page) => page.data.artists) ?? [];
  const albums = data?.pages.flatMap((page) => page.data.albums) ?? [];
  const tracks = data?.pages.flatMap((page) => page.data.tracks) ?? [];

  const hasResults = artists.length > 0 || albums.length > 0 || tracks.length > 0;

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
          scrollEventThrottle={400}
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
              <Music2 size={48} className="text-muted-foreground mb-2" />
              <Text className="text-muted-foreground">No results found</Text>
            </View>
          )}

          {!isLoading && hasResults && (
            <>
              {/* Artists */}
              {artists.length > 0 && (
                <View className="mb-6">
                  <Text variant="h4" className="mb-3">Artists</Text>
                  <View className="gap-2">
                    {artists
                      .slice(0, expandedSections.artists ? undefined : INITIAL_LIMIT)
                      .map((artist) => (
                        <ArtistCard key={artist.id} artist={artist} />
                      ))}
                  </View>
                  {artists.length > INITIAL_LIMIT && !expandedSections.artists && (
                    <Pressable
                      onPress={() => setExpandedSections((prev) => ({ ...prev, artists: true }))}
                      className="mt-3 py-2 items-center"
                    >
                      <Text className="text-primary font-medium">
                        View all {artists.length} artists
                      </Text>
                    </Pressable>
                  )}
                </View>
              )}

              {/* Albums */}
              {albums.length > 0 && (
                <View className="mb-6">
                  <Text variant="h4" className="mb-3">Albums</Text>
                  <View className="flex-row flex-wrap -mx-1">
                    {albums
                      .slice(0, expandedSections.albums ? undefined : INITIAL_ALBUMS_LIMIT)
                      .map((album) => {
                        const artworkPath = album.artworks?.small ?? '';
                        const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;
                        const artistNames = album.artists && album.artists.length > 4
                          ? 'Various Artists'
                          : album.artists?.map(a => a.name).join(', ') ?? 'Unknown Artist';

                        return (
                          <View key={album.id} className="basis-full sm:basis-1/2 md:basis-1/3 lg:basis-1/4 xl:basis-1/5 2xl:basis-1/6 px-1 mb-2">
                            <Link href={`/albums/${album.id}`} asChild>
                              <Pressable className="flex-row gap-2.5 web:hover:bg-muted/30 active:bg-muted/50 rounded-lg p-1">
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
                                <View className="flex-1 justify-center min-w-0">
                                  <Text className="font-medium text-sm" numberOfLines={1}>{album.name}</Text>
                                  <Text className="text-muted-foreground text-xs" numberOfLines={1}>{artistNames}</Text>
                                  {album.releaseYear && (
                                    <Text className="text-muted-foreground text-xs">{album.releaseYear}</Text>
                                  )}
                                </View>
                              </Pressable>
                            </Link>
                          </View>
                        );
                      })}
                  </View>
                  {albums.length > INITIAL_ALBUMS_LIMIT && !expandedSections.albums && (
                    <Pressable
                      onPress={() => setExpandedSections((prev) => ({ ...prev, albums: true }))}
                      className="mt-3 py-2 items-center"
                    >
                      <Text className="text-primary font-medium">
                        View all {albums.length} albums
                      </Text>
                    </Pressable>
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
              <Music2 size={48} className="text-muted-foreground mb-2" />
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
