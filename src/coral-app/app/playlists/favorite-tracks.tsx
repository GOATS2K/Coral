import { ColorValue, Platform, ScrollView, View } from 'react-native';
import { Stack } from 'expo-router';
import { useFavoriteTracks, useReorderFavoriteTrack } from '@/lib/client/components';
import { Text } from '@/components/ui/text';
import { Button } from '@/components/ui/button';
import { LinearGradient } from 'expo-linear-gradient';
import { BlurView } from 'expo-blur';
import { useAtomValue } from 'jotai';
import { themeAtom, playerStateAtom, PlaybackSource } from '@/lib/state';
import { ReorderableTrackListing, ReorderableTrack } from '@/components/reorderable-track-listing';
import { Heart, Play, Pause } from 'lucide-react-native';
import { usePlayer } from '@/lib/player/use-player';
import { Icon } from '@/components/ui/icon';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { DebouncedLoader } from '@/components/debounced-loader';
import { useQueryClient } from '@tanstack/react-query';

const SCREEN_OPTIONS = {
  headerShown: false
};

function FavoriteTracksHeader() {
  const theme = useAtomValue(themeAtom);
  const { play, togglePlayPause, isPlaying } = usePlayer();
  const playerState = useAtomValue(playerStateAtom);
  const { data: playlist } = useFavoriteTracks({});

  // Check if favorites are currently playing
  const isFavoritesPlaying =
    playerState.initializer?.source === PlaybackSource.Favorites &&
    isPlaying;

  const handlePlayPause = () => {
    if (playerState.initializer?.source === PlaybackSource.Favorites) {
      // Favorites already playing - toggle play/pause
      togglePlayPause();
    } else {
      // Start playing favorites from beginning
      const tracks = playlist?.tracks?.map(pt => pt.track);
      if (tracks && tracks.length > 0) {
        play(tracks, 0, {
          source: PlaybackSource.Favorites,
          id: 'tracks',
        });
      }
    }
  };

  // Get background color based on theme
  const backgroundColor = theme === 'dark' ? 'hsl(0, 0%, 3.9%)' : 'hsl(0, 0%, 100%)';

  // Static gradient colors for favorites (light blue theme)
  const gradientColors = ['#60a5fa80', '#3b82f680', backgroundColor] as ColorValue[];
  const gradientLocations = [0, 0.3, 1];

  const trackCount = playlist?.tracks?.length || 0;

  return (
    <View className="overflow-hidden">
      <LinearGradient
        colors={gradientColors}
        locations={gradientLocations}
        className="absolute inset-0"
      />
      <BlurView
        intensity={60}
        tint="dark"
        className="flex-1"
      >
        <View className="flex-col sm:flex-row gap-4 px-4 py-6 items-center sm:items-start">
          {/* Heart Icon */}
          <View className="mx-auto sm:mx-0 w-[200px] h-[200px] rounded-lg overflow-hidden">
            <View className="w-full h-full items-center justify-center bg-muted">
              <Icon as={Heart} size={100} className="text-muted-foreground opacity-40" fill="currentColor" />
            </View>
          </View>

          {/* Playlist Info */}
          <View className="flex-1 flex-col items-center sm:items-start sm:h-[200px] sm:justify-between">
            <View className="gap-1">
              {/* Playlist Title */}
              <Text variant="h4" className="text-white font-bold text-center sm:text-left drop-shadow-lg">
                Favorite Tracks
              </Text>

              {/* Metadata */}
              <Text variant="small" className="text-white/90 text-center sm:text-left drop-shadow-md mt-1">
                {trackCount} {trackCount === 1 ? 'track' : 'tracks'}
              </Text>
            </View>

            {/* Action Buttons */}
            <View className="mt-4 flex-row gap-2">
              <Button
                onPress={handlePlayPause}
                className="bg-white web:hover:bg-white/90 active:bg-white/80"
                disabled={trackCount === 0}
              >
                {isFavoritesPlaying ? (
                  <>
                    <Icon as={Pause} size={18} className="text-black" fill="currentColor" />
                    <Text className="text-black font-medium">Pause</Text>
                  </>
                ) : (
                  <>
                    <Icon as={Play} size={18} className="text-black" fill="currentColor" />
                    <Text className="text-black font-medium">Play</Text>
                  </>
                )}
              </Button>
            </View>
          </View>
        </View>
      </BlurView>
    </View>
  );
}

export default function FavoriteTracksScreen() {
  const { data: playlist, isLoading, error } = useFavoriteTracks({});
  const insets = useSafeAreaInsets();
  const reorderMutation = useReorderFavoriteTrack();
  const queryClient = useQueryClient();

  const handleReorder = async (fromIndex: number, toIndex: number) => {
    if (!playlist) return;

    const playlistTrack = playlist.tracks[fromIndex];
    if (!playlistTrack) return;

    try {
      await reorderMutation.mutateAsync({
        pathParams: { playlistTrackId: playlistTrack.id },
        body: toIndex,
      });
      await queryClient.invalidateQueries();
    } catch (err) {
      console.error('Failed to reorder track:', err);
    }
  };

  if (error) {
    return (
      <>
        <Stack.Screen options={SCREEN_OPTIONS} />
        <View className="flex-1 items-center justify-center gap-8 p-4 bg-background">
          <Text className="text-destructive">Error loading favorite tracks</Text>
        </View>
      </>
    );
  }

  if (isLoading) {
    return (
      <>
        <Stack.Screen options={SCREEN_OPTIONS} />
        <DebouncedLoader isLoading={isLoading}>
          <Text className="text-muted-foreground">Loading...</Text>
        </DebouncedLoader>
      </>
    );
  }

  const hasTracks = playlist && playlist.tracks && playlist.tracks.length > 0;

  // Transform PlaylistTrackDto[] to ReorderableTrack[]
  const reorderableTracks: ReorderableTrack[] = playlist?.tracks?.map(pt => ({
    id: pt.id,
    track: pt.track,
  })) || [];

  return (
    <>
      <Stack.Screen options={SCREEN_OPTIONS} />
      <View className="flex-1 bg-background" style={{ paddingTop: Platform.OS === 'web' ? 0 : insets.top }}>
        <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
          <FavoriteTracksHeader />
          {hasTracks ? (
            <ReorderableTrackListing
              tracks={reorderableTracks}
              onReorder={handleReorder}
              className="px-4 sm:px-6 pb-20 mt-6"
              initializer={{ source: PlaybackSource.Favorites, id: 'tracks' }}
              showTrackNumber
            />
          ) : (
            <View className="flex-1 items-center justify-center py-16 px-4">
              <Icon as={Heart} size={64} className="text-muted-foreground mb-4" />
              <Text variant="h3" className="text-center mb-2">No favorite tracks yet</Text>
              <Text className="text-muted-foreground text-center">
                Start liking tracks to see them here
              </Text>
            </View>
          )}
        </ScrollView>
      </View>
    </>
  );
}
