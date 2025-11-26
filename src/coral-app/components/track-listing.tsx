import { Pressable, View, Image } from 'react-native';
import { Text } from '@/components/ui/text';
import { SimpleTrackDto, AlbumDto, PlaylistTrackDto } from '@/lib/client/schemas';
import { usePlayerActions } from '@/lib/player/use-player';
import { baseUrl } from '@/lib/client/fetcher';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
import { useState } from 'react';
import { TrackMenu } from '@/components/track-menu';
import { PlaybackInitializer, playerStateAtom } from '@/lib/state';
import { useAtomValue } from 'jotai';

interface TrackListingProps {
  tracks: SimpleTrackDto[];
  album?: AlbumDto;
  showTrackNumber?: boolean;
  showCoverArt?: boolean;
  className?: string;
  initializer?: PlaybackInitializer;
}

function TrackArtwork({ albumId }: { albumId: string }) {
  const [imageError, setImageError] = useState(false);

  if (imageError || !albumId) {
    return <MissingAlbumCover size={16} />;
  }

  return (
    <Image
      source={{ uri: `${baseUrl}/api/artwork?albumId=${albumId}&size=small` }}
      className="w-full h-full"
      resizeMode="cover"
      onError={() => setImageError(true)}
    />
  );
}

interface TrackRowProps {
  track: SimpleTrackDto;
  index: number;
  isActive: boolean;
  showCoverArt: boolean;
  showTrackNumber: boolean;
  hasMultipleDiscs: boolean;
  onPlay: (index: number) => void;
}

function TrackRow({
  track,
  index,
  isActive,
  showCoverArt,
  showTrackNumber,
  hasMultipleDiscs,
  onPlay
}: TrackRowProps) {
  const formatDuration = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  const formatTrackNumber = (track: SimpleTrackDto, index: number) => {
    // Use track number if available, otherwise fall back to index + 1
    const trackNum = track.trackNumber || (index + 1);
    const num = trackNum.toString().padStart(2, '0');
    return hasMultipleDiscs ? `${track.discNumber || 1}.${num}` : num;
  };

  return (
    <TrackMenu key={track.id} track={track} isActive={isActive}>
      <Pressable
        onPress={() => onPlay(index)}
        className="flex-row py-2 items-center gap-2 web:cursor-pointer active:bg-muted/50 web:hover:bg-muted/30 -mx-4 px-4 sm:-mx-6 sm:px-6"
      >
        {showCoverArt ? (
          <View className="w-10 h-10 rounded overflow-hidden">
            <TrackArtwork albumId={track.album?.id || ''} />
          </View>
        ) : showTrackNumber ? (
          <Text variant="small" className={`w-8 select-none text-xs ${isActive ? 'text-orange-700 dark:text-orange-500 font-bold' : 'text-muted-foreground'}`}>
            {formatTrackNumber(track, index)}
          </Text>
        ) : null}
        <View className="flex-1 min-w-0">
          <Text variant="default" className={`select-none leading-tight text-sm ${isActive ? 'text-orange-700 dark:text-orange-500 font-bold' : 'text-foreground'}`} numberOfLines={1}>
            {track.title}
          </Text>
          <Text variant="small" className={`mt-0.5 select-none leading-tight text-xs ${isActive ? 'text-orange-700/80 dark:text-orange-500/80 font-semibold' : 'text-muted-foreground'}`} numberOfLines={1}>
            {track.artists.filter(a => a.role === 'Main').map(a => a.name).join(', ')}
          </Text>
        </View>
        <Text variant="small" className={`hidden sm:block w-12 text-right select-none text-xs ${isActive ? 'text-orange-700 dark:text-orange-500 font-bold' : 'text-muted-foreground'}`}>
          {formatDuration(track.durationInSeconds)}
        </Text>
      </Pressable>
    </TrackMenu>
  );
}

export function TrackListing({ tracks, album, showTrackNumber = true, showCoverArt = false, className, initializer }: TrackListingProps) {
  const { play } = usePlayerActions();
  const activeTrack = useAtomValue(playerStateAtom).currentTrack;

  const hasMultipleDiscs = new Set(tracks.map(t => t.discNumber || 1)).size > 1;

  const handlePlay = (index: number) => {
    play(tracks, index, initializer);
  };

  return (
    <>
      <View className={className}>
        {tracks.map((track, index) => {
          const isActive = activeTrack?.id === track.id;

          return (
            <TrackRow
              key={track.id}
              track={track}
              index={index}
              isActive={isActive}
              showCoverArt={showCoverArt}
              showTrackNumber={showTrackNumber}
              hasMultipleDiscs={hasMultipleDiscs}
              onPlay={handlePlay}
            />
          );
        })}
      </View>
    </>
  );
}