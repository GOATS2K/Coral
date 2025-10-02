import { Platform, View, Image, Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { usePlayer } from '@/lib/player/use-player';
import { Play, Pause, SkipBack, SkipForward, Volume2, VolumeX, ListMusic } from 'lucide-react-native';
import { baseUrl } from '@/lib/client/fetcher';
import { Link } from 'expo-router';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { useState, useEffect } from 'react';

export function WebPlayerBar() {
  // Only render on web
  if (Platform.OS !== 'web') {
    return null;
  }

  const { activeTrack, isPlaying, progress, volume, isMuted, queue, currentIndex, togglePlayPause, skip, seekTo, setVolume, toggleMute, reorderQueue } = usePlayer();
  const [isDragging, setIsDragging] = useState(false);
  const [dragPosition, setDragPosition] = useState(0);
  const [localVolume, setLocalVolume] = useState(1);
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  // Initialize local volume from player once
  useEffect(() => {
    setLocalVolume(volume);
  }, []);

  const handleVolumeChange = (e: any) => {
    const newVolume = parseFloat(e.target.value);
    setLocalVolume(newVolume);
    setVolume(newVolume);
  };

  // Media Session API - update metadata when track changes
  useEffect(() => {
    if (!('mediaSession' in navigator) || !activeTrack) return;

    const artworkUrl = activeTrack.album?.id
      ? `${baseUrl}/api/artwork?albumId=${activeTrack.album.id}&size=small`
      : null;

    const artistNames = activeTrack.artists
      ?.filter(a => a.role === 'Main')
      .map(a => a.name)
      .join(', ') || 'Unknown Artist';

    navigator.mediaSession.metadata = new MediaMetadata({
      title: activeTrack.title,
      artist: artistNames,
      album: activeTrack.album?.name || '',
      artwork: artworkUrl ? [
        { src: artworkUrl, sizes: '300x300', type: 'image/jpeg' }
      ] : [],
    });
  }, [activeTrack]);

  // Media Session API - register handlers once
  useEffect(() => {
    if (!('mediaSession' in navigator)) return;

    navigator.mediaSession.setActionHandler('play', () => togglePlayPause());
    navigator.mediaSession.setActionHandler('pause', () => togglePlayPause());
    navigator.mediaSession.setActionHandler('previoustrack', () => skip(-1));
    navigator.mediaSession.setActionHandler('nexttrack', () => skip(1));
    navigator.mediaSession.setActionHandler('seekto', (details) => {
      if (details.seekTime !== undefined) seekTo(details.seekTime);
    });

    return () => {
      if ('mediaSession' in navigator) {
        navigator.mediaSession.setActionHandler('play', null);
        navigator.mediaSession.setActionHandler('pause', null);
        navigator.mediaSession.setActionHandler('previoustrack', null);
        navigator.mediaSession.setActionHandler('nexttrack', null);
        navigator.mediaSession.setActionHandler('seekto', null);
      }
    };
  }, [togglePlayPause, skip, seekTo]);

  // Don't show bar if no track is active
  if (!activeTrack) {
    return null;
  }

  const duration = progress.duration || activeTrack.durationInSeconds || 0;
  const currentPosition = isDragging ? dragPosition : progress.position;

  // Get artwork URL using albumId from track
  const artworkUrl = activeTrack.album?.id
    ? `${baseUrl}/api/artwork?albumId=${activeTrack.album.id}&size=small`
    : null;

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

  const handleDragStart = (index: number) => (e: any) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragOver = (index: number) => (e: any) => {
    e.preventDefault();
    if (draggedIndex === null || draggedIndex === index) return;
    setDragOverIndex(index);
  };

  const handleDrop = (index: number) => (e: any) => {
    e.preventDefault();
    if (draggedIndex === null || draggedIndex === index) return;
    reorderQueue(draggedIndex, index);
    setDraggedIndex(null);
    setDragOverIndex(null);
  };

  const handleDragEnd = () => {
    setDraggedIndex(null);
    setDragOverIndex(null);
  };

  const handleSeekChange = (e: any) => {
    setDragPosition(parseFloat(e.target.value));
  };

  const handleSeekStart = () => {
    setIsDragging(true);
  };

  const handleSeekEnd = (e: any) => {
    const position = parseFloat(e.target.value);
    setIsDragging(false);
    seekTo(position);
  };

  return (
    <View className="bg-card border-t border-border h-24 flex-row items-center px-4 py-3 gap-4">
      {/* Album Art & Track Info */}
      <View className="flex-row items-center gap-3 flex-1 min-w-0">
        <Tooltip delayDuration={200}>
          <Link href={`/albums/${activeTrack.album?.id}`} asChild>
            <TooltipTrigger asChild>
              <Pressable>
                {artworkUrl ? (
                  <Image
                    source={{ uri: artworkUrl }}
                    className="w-14 h-14 rounded"
                    resizeMode="cover"
                  />
                ) : (
                  <View className="w-14 h-14 rounded bg-muted" />
                )}
              </Pressable>
            </TooltipTrigger>
          </Link>
          <TooltipContent>
            <Text>Go to Album</Text>
          </TooltipContent>
        </Tooltip>
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
            onChange={handleSeekChange}
            onMouseDown={handleSeekStart}
            onMouseUp={handleSeekEnd}
            onTouchStart={handleSeekStart}
            onTouchEnd={handleSeekEnd}
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

      {/* Queue & Volume Controls */}
      <View className="flex-row items-center gap-5 flex-1 justify-end">
        {/* Queue Button */}
        <Popover>
          <PopoverTrigger asChild>
            <Pressable className="web:hover:opacity-70 active:opacity-50">
              <ListMusic size={20} className="text-foreground" />
            </Pressable>
          </PopoverTrigger>
          <PopoverContent className="w-96 max-h-96 overflow-hidden p-0" align="end">
            <View className="p-3 border-b border-border">
              <Text className="font-semibold">Queue ({queue.length})</Text>
            </View>
            <View className="overflow-y-auto max-h-80">
              {queue.map((track, index) => {
                const trackArtworkUrl = track.album?.id
                  ? `${baseUrl}/api/artwork?albumId=${track.album.id}&size=small`
                  : null;
                const trackArtists = track.artists
                  ?.filter(a => a.role === 'Main')
                  .map(a => a.name)
                  .join(', ') || 'Unknown Artist';
                const isCurrentTrack = index === currentIndex;
                const isDraggedOver = dragOverIndex === index;

                return (
                  <div
                    key={track.id}
                    draggable
                    onDragStart={handleDragStart(index)}
                    onDragOver={handleDragOver(index)}
                    onDrop={handleDrop(index)}
                    onDragEnd={handleDragEnd}
                    className={`flex flex-row items-center gap-3 p-2 cursor-grab active:cursor-grabbing ${isCurrentTrack ? 'bg-accent/50' : ''} ${isDraggedOver ? 'border-t-2 border-primary' : ''} hover:bg-accent/30 transition-colors`}
                    style={{
                      opacity: draggedIndex === index ? 0.5 : 1,
                      userSelect: 'none',
                      WebkitUserSelect: 'none',
                    }}
                  >
                    {trackArtworkUrl ? (
                      <Image
                        source={{ uri: trackArtworkUrl }}
                        className="w-10 h-10 rounded"
                        resizeMode="cover"
                      />
                    ) : (
                      <View className="w-10 h-10 rounded bg-muted" />
                    )}
                    <View className="flex-1 min-w-0">
                      <Text className="font-medium text-sm" numberOfLines={1}>
                        {track.title}
                      </Text>
                      <Text className="text-muted-foreground text-xs" numberOfLines={1}>
                        {trackArtists}
                      </Text>
                    </View>
                    {isCurrentTrack && (
                      <Text className="text-xs text-primary font-semibold">Playing</Text>
                    )}
                  </div>
                );
              })}
            </View>
          </PopoverContent>
        </Popover>

        {/* Volume Controls */}
        <View className="flex-row items-center gap-2">
          <Pressable onPress={toggleMute} className="web:hover:opacity-70 active:opacity-50">
            {isMuted ? (
              <VolumeX size={20} className="text-muted-foreground" />
            ) : (
              <Volume2 size={20} className="text-foreground" />
            )}
          </Pressable>
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={localVolume}
            onChange={handleVolumeChange}
            className={`w-24 h-1 accent-primary cursor-pointer appearance-none rounded-full [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3 [&::-webkit-slider-thumb]:h-3 [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-primary [&::-webkit-slider-thumb]:opacity-0 [&::-webkit-slider-thumb]:transition-opacity hover:[&::-webkit-slider-thumb]:opacity-100 [&::-moz-range-thumb]:w-3 [&::-moz-range-thumb]:h-3 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-primary [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:opacity-0 [&::-moz-range-thumb]:transition-opacity hover:[&::-moz-range-thumb]:opacity-100 ${isMuted ? 'opacity-40' : ''}`}
            style={{
              background: `linear-gradient(to right, hsl(var(--primary)) 0%, hsl(var(--primary)) ${localVolume * 100}%, hsl(var(--muted)) ${localVolume * 100}%, hsl(var(--muted)) 100%)`,
            }}
          />
        </View>
      </View>
    </View>
  );
}