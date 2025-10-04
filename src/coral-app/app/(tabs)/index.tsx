import { View, ScrollView, ActivityIndicator, Pressable, Image } from 'react-native';
import { Stack, Link } from 'expo-router';
import { Text } from '@/components/ui/text';
import { useFavoriteTracks, useFavoriteArtists, useFavoriteAlbums, usePaginatedAlbums } from '@/lib/client/components';
import { TrackListing } from '@/components/track-listing';
import { AlbumCard } from '@/components/album-card';
import { ArtistCard } from '@/components/artist-card';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
import { baseUrl } from '@/lib/client/fetcher';
import { Heart } from 'lucide-react-native';
import { PlaybackSource } from '@/lib/state';

const SCREEN_OPTIONS = {
  headerShown: false
};

export default function HomeScreen() {
  const { data: favoriteTracks, isLoading: tracksLoading } = useFavoriteTracks({});
  const { data: favoriteArtists, isLoading: artistsLoading } = useFavoriteArtists({});
  const { data: favoriteAlbums, isLoading: albumsLoading } = useFavoriteAlbums({});
  const { data: recentAlbums, isLoading: recentAlbumsLoading } = usePaginatedAlbums({
    queryParams: { limit: 12, offset: 0 },
  });

  const isLoading = tracksLoading || artistsLoading || albumsLoading || recentAlbumsLoading;

  const hasFavorites =
    (favoriteTracks && favoriteTracks.length > 0) ||
    (favoriteArtists && favoriteArtists.length > 0) ||
    (favoriteAlbums && favoriteAlbums.length > 0);

  if (isLoading) {
    return (
      <>
        <Stack.Screen options={SCREEN_OPTIONS} />
        <View className="flex-1 items-center justify-center bg-background">
          <ActivityIndicator size="large" />
        </View>
      </>
    );
  }

  return (
    <>
      <Stack.Screen options={SCREEN_OPTIONS} />
      <View className="flex-1 bg-background">
        <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
          {/* Recently Added Section */}
          {recentAlbums && recentAlbums.data.length > 0 && (
            <View className="mb-8">
              <Text variant="h2" className="px-4 pt-8 mb-4 font-bold">Recently Added</Text>
              <View className="px-4 flex-row flex-wrap -mx-1">
                {recentAlbums.data.map((album) => {
                  const artworkPath = album.artworks?.small ?? '';
                  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;
                  const artistNames = album.artists && album.artists.length > 4
                    ? 'Various Artists'
                    : album.artists?.map(a => a.name).join(', ') ?? 'Unknown Artist';

                  return (
                    <View key={album.id} className="basis-full md:basis-1/2 lg:basis-1/3 px-1 mb-2">
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
            </View>
          )}

          {/* Favorites Section */}
          {hasFavorites && (
            <View className="px-4 pb-4">
              <Text variant="h2" className="font-bold">Favorites</Text>
            </View>
          )}

          {!hasFavorites && !recentAlbums?.data.length && (
            <View className="flex-1 items-center justify-center py-16 px-4">
              <Heart size={64} className="text-muted-foreground mb-4" />
              <Text variant="h3" className="text-center mb-2">No favorites yet</Text>
              <Text className="text-muted-foreground text-center">
                Start liking tracks, albums, and artists to see them here
              </Text>
            </View>
          )}

          {/* Favorite Artists */}
          {favoriteArtists && favoriteArtists.length > 0 && (
            <View className="mb-8 px-4">
              <Text variant="h3" className="mb-4">Favorite Artists</Text>
              <View className="gap-2">
                {favoriteArtists.map((artist) => (
                  <ArtistCard key={artist.id} artist={artist} />
                ))}
              </View>
            </View>
          )}

          {/* Favorite Albums */}
          {favoriteAlbums && favoriteAlbums.length > 0 && (
            <View className="mb-8">
              <Text variant="h3" className="px-4 mb-4">Favorite Albums</Text>
              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                contentContainerStyle={{ paddingHorizontal: 8 }}
              >
                {favoriteAlbums.map((album) => (
                  <View key={album.id} style={{ width: 198 }}>
                    <AlbumCard album={album} />
                  </View>
                ))}
              </ScrollView>
            </View>
          )}

          {/* Favorite Tracks */}
          {favoriteTracks && favoriteTracks.length > 0 && (
            <View className="mb-8 px-4">
              <Text variant="h3" className="mb-4">Favorite Tracks</Text>
              <TrackListing
                tracks={favoriteTracks}
                showTrackNumber={false}
                showCoverArt={true}
                initializer={{ source: PlaybackSource.Favorites, id: 'tracks' }}
              />
            </View>
          )}

          {/* Bottom Padding */}
          <View className="pb-20" />
        </ScrollView>
      </View>
    </>
  );
}
