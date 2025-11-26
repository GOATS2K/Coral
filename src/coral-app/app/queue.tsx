import { ColorValue, Platform, ScrollView, View } from 'react-native';
import { Stack } from 'expo-router';
import { Text } from '@/components/ui/text';
import { Button } from '@/components/ui/button';
import { LinearGradient } from 'expo-linear-gradient';
import { BlurView } from 'expo-blur';
import { useAtomValue, useSetAtom } from 'jotai';
import { themeAtom, playerStateAtom } from '@/lib/state';
import { ReorderableTrackListing, ReorderableTrack } from '@/components/reorderable-track-listing';
import { ListMusic, Play, Pause, Shuffle, Trash2 } from 'lucide-react-native';
import { usePlayer } from '@/lib/player/use-player';
import { Icon } from '@/components/ui/icon';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const SCREEN_OPTIONS = {
  headerShown: false,
};

function QueueHeader() {
  const theme = useAtomValue(themeAtom);
  const { togglePlayPause, isPlaying } = usePlayer();
  const playerState = useAtomValue(playerStateAtom);
  const setState = useSetAtom(playerStateAtom);

  const toggleShuffle = () => {
    if (playerState.isShuffled) {
      setState({ type: 'unshuffle' });
    } else {
      setState({ type: 'shuffle' });
    }
  };

  const backgroundColor =
    theme === 'dark' ? 'hsl(0, 0%, 3.9%)' : 'hsl(0, 0%, 100%)';

  // Purple/indigo gradient for queue
  const gradientColors = [
    '#8b5cf680',
    '#6366f180',
    backgroundColor,
  ] as ColorValue[];
  const gradientLocations = [0, 0.3, 1];

  const trackCount = playerState.queue.length;
  const isShuffled = playerState.isShuffled;

  const handleClearQueue = () => {
    setState({ type: 'setQueue', queue: [], index: 0 });
  };

  return (
    <View className="overflow-hidden">
      <LinearGradient
        colors={gradientColors}
        locations={gradientLocations}
        className="absolute inset-0"
      />
      <BlurView intensity={60} tint="dark" className="flex-1">
        <View className="flex-col sm:flex-row gap-4 px-4 py-6 items-center sm:items-start">
          {/* Queue Icon */}
          <View className="mx-auto sm:mx-0 w-[200px] h-[200px] rounded-lg overflow-hidden">
            <View className="w-full h-full items-center justify-center bg-muted">
              <Icon
                as={ListMusic}
                size={100}
                className="text-muted-foreground opacity-40"
              />
            </View>
          </View>

          {/* Queue Info */}
          <View className="flex-1 flex-col items-center sm:items-start sm:h-[200px] sm:justify-between">
            <View className="gap-1">
              <Text
                variant="h4"
                className="text-white font-bold text-center sm:text-left drop-shadow-lg"
              >
                Queue
              </Text>

              <Text
                variant="small"
                className="text-white/90 text-center sm:text-left drop-shadow-md mt-1"
              >
                {trackCount} {trackCount === 1 ? 'track' : 'tracks'}
                {isShuffled && ' • Shuffled'}
              </Text>
            </View>

            {/* Action Buttons */}
            <View className="mt-4 flex-row gap-2">
              <Button
                onPress={togglePlayPause}
                className="bg-white web:hover:bg-white/90 active:bg-white/80"
                disabled={trackCount === 0}
              >
                {isPlaying ? (
                  <>
                    <Icon
                      as={Pause}
                      size={18}
                      className="text-black"
                      fill="currentColor"
                    />
                    <Text className="text-black font-medium">Pause</Text>
                  </>
                ) : (
                  <>
                    <Icon
                      as={Play}
                      size={18}
                      className="text-black"
                      fill="currentColor"
                    />
                    <Text className="text-black font-medium">Play</Text>
                  </>
                )}
              </Button>
              <Button
                onPress={toggleShuffle}
                variant="outline"
                className={`border-white bg-white/10 web:hover:bg-white/20 active:bg-white/30 ${
                  isShuffled ? 'bg-white/30' : ''
                }`}
                disabled={trackCount === 0}
              >
                <Icon as={Shuffle} size={18} className="text-white" />
                <Text className="text-white font-medium">Shuffle</Text>
              </Button>
              <Button
                onPress={handleClearQueue}
                variant="outline"
                className="border-white bg-white/10 web:hover:bg-white/20 active:bg-white/30"
                disabled={trackCount === 0}
              >
                <Icon as={Trash2} size={18} className="text-white" />
                <Text className="text-white font-medium">Clear</Text>
              </Button>
            </View>
          </View>
        </View>
      </BlurView>
    </View>
  );
}

export default function QueueScreen() {
  const playerState = useAtomValue(playerStateAtom);
  const setState = useSetAtom(playerStateAtom);
  const { playFromIndex } = usePlayer();
  const insets = useSafeAreaInsets();

  const handleReorder = (fromIndex: number, toIndex: number) => {
    setState({ type: 'reorder', from: fromIndex, to: toIndex });
  };

  const hasTracks = playerState.queue.length > 0;

  // Transform SimpleTrackDto[] to ReorderableTrack[]
  const reorderableTracks: ReorderableTrack[] = playerState.queue.map(
    (track, index) => ({
      id: `${track.id}-${index}`,
      track,
    })
  );

  return (
    <>
      <Stack.Screen options={SCREEN_OPTIONS} />
      <View
        className="flex-1 bg-background"
        style={{ paddingTop: Platform.OS === 'web' ? 0 : insets.top }}
      >
        <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
          <QueueHeader />
          {hasTracks ? (
            <ReorderableTrackListing
              tracks={reorderableTracks}
              onReorder={handleReorder}
              onPlay={playFromIndex}
              className="px-4 sm:px-6 pb-20 mt-6"
            />
          ) : (
            <View className="flex-1 items-center justify-center py-16 px-4">
              <Icon
                as={ListMusic}
                size={64}
                className="text-muted-foreground mb-4"
              />
              <Text variant="h3" className="text-center mb-2">
                Queue is empty
              </Text>
              <Text className="text-muted-foreground text-center">
                Play some music to see it here
              </Text>
            </View>
          )}
        </ScrollView>
      </View>
    </>
  );
}
