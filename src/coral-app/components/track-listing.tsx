import { Pressable, View, Platform, Image } from 'react-native';
import { Text } from '@/components/ui/text';
import { SimpleTrackDto, AlbumDto } from '@/lib/client/schemas';
import { usePlayer } from '@/lib/player/use-player';
import { Sparkles, Plus, User, Heart } from 'lucide-react-native';
import { useToast } from '@/lib/hooks/use-toast';
import { useSetAtom } from 'jotai';
import { playerStateAtom, PlaybackInitializer } from '@/lib/state';
import { addToQueue, findSimilarAndAddToQueue } from '@/lib/player/player-queue-utils';
import { baseUrl } from '@/lib/client/fetcher';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
import { useRouter } from 'expo-router';
import { useFavoriteTrack, useRemoveFavoriteTrack } from '@/lib/client/components';
import { useQueryClient } from '@tanstack/react-query';
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger,
} from '@/components/ui/context-menu';

interface TrackListingProps {
  tracks: SimpleTrackDto[];
  album?: AlbumDto;
  showTrackNumber?: boolean;
  showCoverArt?: boolean;
  className?: string;
  initializer?: PlaybackInitializer;
}

export function TrackListing({ tracks, album, showTrackNumber = true, showCoverArt = false, className, initializer }: TrackListingProps) {
  const { play, activeTrack } = usePlayer();
  const setState = useSetAtom(playerStateAtom);
  const { showToast } = useToast();
  const router = useRouter();
  const queryClient = useQueryClient();
  const favoriteMutation = useFavoriteTrack();
  const removeFavoriteMutation = useRemoveFavoriteTrack();

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

  const handleFindSimilar = async (trackId: string) => {
    await findSimilarAndAddToQueue(trackId, setState, showToast);
  };

  const handleAddToQueue = (track: SimpleTrackDto) => {
    addToQueue(setState, track);
    showToast(`Added "${track.title}" to queue`);
  };

  const handleGoToArtist = (artistId: string) => {
    router.push(`/artists/${artistId}`);
  };

  const handleLikeTrack = async (trackId: string, trackTitle: string, isFavorited: boolean) => {
    try {
      if (isFavorited) {
        await removeFavoriteMutation.mutateAsync({
          pathParams: { trackId },
        });
        showToast(`Removed "${trackTitle}" from favorites`);
      } else {
        await favoriteMutation.mutateAsync({
          pathParams: { trackId },
        });
        showToast(`Liked "${trackTitle}"`);
      }

      // Invalidate all queries that might contain this track
      await queryClient.invalidateQueries();
    } catch (error) {
      showToast(isFavorited ? 'Failed to remove favorite' : 'Failed to like track');
      console.error('Error toggling favorite:', error);
    }
  };

  return (
    <>
      <View className={className}>
        {tracks.map((track, index) => {
          const isActive = activeTrack?.id === track.id;
          const mainArtists = track.artists.filter(a => a.role === 'Main');

          return (
            <ContextMenu key={track.id}>
              <ContextMenuTrigger>
                <Pressable
                  onPress={() => play(tracks, index, initializer)}
                  className={`flex-row py-2 items-center gap-2 web:cursor-pointer active:bg-muted/50 web:hover:bg-muted/30 rounded-md -mx-2 px-2 ${isActive ? 'bg-primary/10' : ''}`}
                >
                  {showCoverArt ? (
                    <View className="w-10 h-10 rounded overflow-hidden">
                      {track.album?.artworks?.small ? (
                        <Image
                          source={{ uri: `${baseUrl}${track.album.artworks.small}` }}
                          className="w-full h-full"
                          resizeMode="cover"
                        />
                      ) : (
                        <MissingAlbumCover size={16} />
                      )}
                    </View>
                  ) : showTrackNumber ? (
                    <Text variant="small" className={`w-8 select-none text-xs ${isActive ? 'text-primary font-medium' : 'text-muted-foreground'}`}>
                      {formatTrackNumber(track)}
                    </Text>
                  ) : null}
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
              </ContextMenuTrigger>

              <ContextMenuContent className="w-56">
                <ContextMenuItem onPress={() => handleLikeTrack(track.id, track.title, track.favorited)}>
                  <Heart size={14} className="text-foreground" fill={track.favorited ? "currentColor" : "none"} />
                  <Text>{track.favorited ? 'Remove from favorites' : 'Like'}</Text>
                </ContextMenuItem>

                <ContextMenuItem onPress={() => handleFindSimilar(track.id)}>
                  <Sparkles size={14} className="text-foreground" />
                  <Text>Find similar songs</Text>
                </ContextMenuItem>

                <ContextMenuItem onPress={() => handleAddToQueue(track)}>
                  <Plus size={14} className="text-foreground" />
                  <Text>Add to queue</Text>
                </ContextMenuItem>

                {/* Go to Artist - with submenu if multiple artists */}
                {mainArtists.length === 1 ? (
                  <ContextMenuItem onPress={() => handleGoToArtist(mainArtists[0].id)}>
                    <User size={14} className="text-foreground" />
                    <Text>Go to artist</Text>
                  </ContextMenuItem>
                ) : mainArtists.length > 1 ? (
                  <ContextMenuSub>
                    <ContextMenuSubTrigger>
                      <User size={14} className="text-foreground" />
                      <Text>Go to artist</Text>
                    </ContextMenuSubTrigger>
                    <ContextMenuSubContent>
                      {mainArtists.map((artist) => (
                        <ContextMenuItem key={artist.id} onPress={() => handleGoToArtist(artist.id)}>
                          <Text>{artist.name}</Text>
                        </ContextMenuItem>
                      ))}
                    </ContextMenuSubContent>
                  </ContextMenuSub>
                ) : null}
              </ContextMenuContent>
            </ContextMenu>
          );
        })}
      </View>
    </>
  );
}