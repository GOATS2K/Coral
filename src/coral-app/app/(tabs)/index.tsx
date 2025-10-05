import { View, ScrollView, ActivityIndicator } from 'react-native';
import { Stack } from 'expo-router';
import { Text } from '@/components/ui/text';
import { useFavoriteArtists, useFavoriteAlbums, usePaginatedAlbums } from '@/lib/client/components';
import { UniversalAlbumCard } from '@/components/universal-album-card';
import { ArtistCard } from '@/components/artist-card';
import { Heart } from 'lucide-react-native';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';
import { Icon } from '@/components/ui/icon';

const SCREEN_OPTIONS = {
  headerShown: false
};

export default function HomeScreen() {
  const theme = useAtomValue(themeAtom);
  const { data: favoriteArtists, isLoading: artistsLoading } = useFavoriteArtists({});
  const { data: favoriteAlbums, isLoading: albumsLoading } = useFavoriteAlbums({});
  const { data: recentAlbums, isLoading: recentAlbumsLoading } = usePaginatedAlbums({
    queryParams: { limit: 12, offset: 0 },
  });

  const isLoading = artistsLoading || albumsLoading || recentAlbumsLoading;

  const hasFavorites =
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
              <View className="px-2 flex-row flex-wrap">
                {recentAlbums.data.map((album) => (
                  <UniversalAlbumCard key={album.id} album={album} />
                ))}
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
            <View className="flex-1 items-center justify-center py-16 px-2">
              <Icon as={Heart} size={64} className="text-muted-foreground mb-4" />
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
              <View className="px-2 flex-row flex-wrap">
                {favoriteAlbums.map((album) => (
                  <UniversalAlbumCard key={album.id} album={album} />
                ))}
              </View>
            </View>
          )}

          {/* Bottom Padding */}
          <View className="pb-20" />
        </ScrollView>
      </View>
    </>
  );
}
