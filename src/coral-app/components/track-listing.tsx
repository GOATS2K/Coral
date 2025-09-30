import { Pressable, View } from 'react-native';
import { Text } from '@/components/ui/text';
import { SimpleTrackDto, AlbumDto } from '@/lib/client/schemas';
import { usePlayer } from '@/lib/player/use-player';

interface TrackListingProps {
  tracks: SimpleTrackDto[];
  album?: AlbumDto;
  showTrackNumber?: boolean;
  className?: string;
}

export function TrackListing({ tracks, album, showTrackNumber = true, className }: TrackListingProps) {
  const { play, activeTrack } = usePlayer();

  const hasMultipleDiscs = new Set(tracks.map(t => t.discNumber || 1)).size > 1;

  const formatDuration = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  const formatTrackNumber = (track: SimpleTrackDto) => {
    const num = track.trackNumber.toString().padStart(2, '0');
    return hasMultipleDiscs ? `${track.discNumber || 1}.${num}` : num;
  };

  return (
    <View className={className}>
      {tracks.map((track, index) => {
        const isActive = activeTrack?.id === track.id;
        return (
          <Pressable
            key={track.id}
            onPress={() => play(tracks, index, album)}
            className={`flex-row py-2 items-center gap-2 web:cursor-pointer active:bg-muted/50 web:hover:bg-muted/30 rounded-md -mx-2 px-2 ${isActive ? 'bg-primary/10' : ''}`}
          >
            {showTrackNumber && (
              <Text variant="small" className={`w-8 select-none text-xs ${isActive ? 'text-primary font-medium' : 'text-muted-foreground'}`}>
                {formatTrackNumber(track)}
              </Text>
            )}
            <View className="flex-1 min-w-0">
              <Text variant="default" className={`select-none leading-tight text-sm ${isActive ? 'text-primary font-medium' : 'text-foreground'}`} numberOfLines={1}>
                {track.title}
              </Text>
              <Text variant="small" className={`mt-0.5 select-none leading-tight text-xs ${isActive ? 'text-primary/80' : 'text-muted-foreground'}`} numberOfLines={1}>
                {track.artists.filter(a => a.role === 'Main').map(a => a.name).join(', ')}
              </Text>
            </View>
            <Text variant="small" className={`hidden sm:block w-12 text-right select-none text-xs ${isActive ? 'text-primary font-medium' : 'text-muted-foreground'}`}>
              {formatDuration(track.durationInSeconds)}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}