import { Platform, View, Image, Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { usePlayer } from '@/lib/player/use-player';
import { Play, Pause, SkipBack, SkipForward } from 'lucide-react-native';
import { baseUrl } from '@/lib/client/fetcher';

export function WebPlayerBar() {
  // Only render on web
  if (Platform.OS !== 'web') {
    return null;
  }

  const { activeTrack, activeAlbum, isPlaying, progress, togglePlayPause, skip, seekTo } = usePlayer();

  console.log('[WebPlayerBar] Render - activeTrack:', activeTrack?.title, 'isPlaying:', isPlaying);

  // Don't show bar if no track is active
  if (!activeTrack) {
    return null;
  }

  const duration = progress.duration || activeTrack.durationInSeconds || 0;
  const currentPosition = progress.position;

  // Get artwork URL - use small size optimized for web player
  const artworkPath = activeAlbum?.artworks?.small ?? "";
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;

  // Get artist names from artists array
  const artistNames = activeTrack.artists
    ?.filter(a => a.role === 'Main')
    .map(a => a.name)
    .join(', ') || 'Unknown Artist';

  const formatTime = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  const handleSeek = (e: any) => {
    seekTo(parseFloat(e.target.value));
  };

  return (
    <View className="fixed bottom-0 left-0 right-0 bg-card border-t border-border h-24 flex-row items-center px-4 py-3 gap-4 z-50">
      {/* Album Art & Track Info */}
      <View className="flex-row items-center gap-3 flex-1 min-w-0">
        {artworkUrl ? (
          <Image
            source={{ uri: artworkUrl }}
            className="w-14 h-14 rounded"
            resizeMode="cover"
          />
        ) : (
          <View className="w-14 h-14 rounded bg-muted" />
        )}
        <View className="flex-1 min-w-0">
          <Text className="text-foreground font-medium select-none" numberOfLines={1}>
            {activeTrack.title}
          </Text>
          <Text className="text-muted-foreground text-sm select-none" numberOfLines={1}>
            {artistNames}
          </Text>
        </View>
      </View>

      {/* Playback Controls & Progress */}
      <View className="flex-1 max-w-2xl flex-col gap-2">
        {/* Control Buttons */}
        <View className="flex-row items-center justify-center gap-4">
          <Pressable onPress={() => skip(-1)} className="web:hover:opacity-70 active:opacity-50">
            <SkipBack size={20} className="text-foreground" />
          </Pressable>
          <Pressable onPress={togglePlayPause} className="web:hover:opacity-70 active:opacity-50 bg-primary rounded-full p-2">
            {isPlaying ? (
              <Pause size={20} className="text-primary-foreground" fill="currentColor" />
            ) : (
              <Play size={20} className="text-primary-foreground" fill="currentColor" />
            )}
          </Pressable>
          <Pressable onPress={() => skip(1)} className="web:hover:opacity-70 active:opacity-50">
            <SkipForward size={20} className="text-foreground" />
          </Pressable>
        </View>

        {/* Progress Bar */}
        <View className="flex-row items-center gap-2">
          <Text className="text-xs text-muted-foreground w-10 text-right select-none">
            {formatTime(currentPosition)}
          </Text>
          <input
            type="range"
            min={0}
            max={duration}
            value={currentPosition}
            onChange={handleSeek}
            className="flex-1 h-1 accent-primary cursor-pointer appearance-none rounded-full [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3 [&::-webkit-slider-thumb]:h-3 [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-primary [&::-webkit-slider-thumb]:opacity-0 [&::-webkit-slider-thumb]:transition-opacity hover:[&::-webkit-slider-thumb]:opacity-100 [&::-moz-range-thumb]:w-3 [&::-moz-range-thumb]:h-3 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-primary [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:opacity-0 [&::-moz-range-thumb]:transition-opacity hover:[&::-moz-range-thumb]:opacity-100"
            style={{
              background: `linear-gradient(to right, hsl(var(--primary)) 0%, hsl(var(--primary)) ${(currentPosition / duration) * 100}%, hsl(var(--muted)) ${(currentPosition / duration) * 100}%, hsl(var(--muted)) 100%)`,
            }}
          />
          <Text className="text-xs text-muted-foreground w-10 select-none">
            {formatTime(duration)}
          </Text>
        </View>
      </View>

      {/* Right spacer for balance */}
      <View className="flex-1" />
    </View>
  );
}