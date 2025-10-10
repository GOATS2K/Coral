import { Platform, Pressable, View } from 'react-native';
import { MoreVertical, Heart, Sparkles, Plus, User, Trash2, Disc3 } from 'lucide-react-native';
import { useRef } from 'react';
import { BottomSheetModal } from '@gorhom/bottom-sheet';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { Text } from '@/components/ui/text';
import { MenuBottomSheet, BottomSheetMenuItem, BottomSheetMenuSeparator } from '@/components/ui/bottom-sheet';
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger,
} from '@/components/ui/context-menu';
import { TrackMenuItems } from '@/components/menu-items/track-menu-items';
import { useToggleFavoriteTrack } from '@/lib/hooks/use-toggle-favorite-track';
import { useRouter } from 'expo-router';
import { useSetAtom, useAtomValue } from 'jotai';
import { playerStateAtom, themeAtom } from '@/lib/state';
import { useToast } from '@/lib/hooks/use-toast';
import { fetchRecommendationsForTrack } from '@/lib/client/components';

interface TrackMenuProps {
  track: SimpleTrackDto;
  children: React.ReactNode;
  isQueueContext?: boolean;
  isActive?: boolean;
}

export function TrackMenu({ track, children, isQueueContext = false, isActive = false }: TrackMenuProps) {
  const bottomSheetRef = useRef<BottomSheetModal>(null);
  const theme = useAtomValue(themeAtom);
  const iconColor = theme === 'dark' ? '#fafafa' : '#09090b';

  const { toggleFavorite } = useToggleFavoriteTrack();
  const router = useRouter();
  const setState = useSetAtom(playerStateAtom);
  const playerState = useAtomValue(playerStateAtom);
  const { showToast } = useToast();

  const isInQueue = playerState.queue.some(t => t.id === track.id);
  const showRemoveFromQueue = isQueueContext && isInQueue;
  const showAddToQueue = !isQueueContext && !isInQueue;

  const handleLike = () => {
    toggleFavorite(track);
    bottomSheetRef.current?.dismiss();
  };

  const handleFindSimilar = async () => {
    bottomSheetRef.current?.dismiss();
    try {
      const recommendations = await fetchRecommendationsForTrack({ pathParams: { trackId: track.id } });
      // Skip first track as it's the track we're getting recommendations for
      const tracksToAdd = recommendations.slice(1);
      setState({ type: 'addMultipleToQueue', tracks: tracksToAdd });
      showToast(`Added ${tracksToAdd.length} similar songs to queue`);
    } catch (err) {
      console.error('Failed to fetch recommendations:', err);
      showToast('Failed to fetch recommendations');
    }
  };

  const handleAddToQueue = () => {
    setState({ type: 'addToQueue', track });
    showToast(`Added "${track.title}" to queue`);
    bottomSheetRef.current?.dismiss();
  };

  const handleRemoveFromQueue = () => {
    // Find the index of the track in the queue
    const index = playerState.queue.findIndex(t => t.id === track.id);
    if (index !== -1) {
      setState({ type: 'removeFromQueue', index });
      showToast(`Removed "${track.title}" from queue`);
    }
    bottomSheetRef.current?.dismiss();
  };

  const handleGoToAlbum = () => {
    bottomSheetRef.current?.dismiss();
    if (track.album?.id) {
      router.push(`/albums/${track.album.id}`);
    }
  };

  const handleGoToArtist = (artistId: string) => {
    bottomSheetRef.current?.dismiss();
    router.push(`/artists/${artistId}`);
  };

  // Web: use ContextMenu
  if (Platform.OS === 'web') {
    return (
      <ContextMenu>
        <ContextMenuTrigger>
          <View className={`-mx-2 px-2 rounded-md ${isActive ? 'bg-primary/10' : ''}`}>
            {children}
          </View>
        </ContextMenuTrigger>
        <ContextMenuContent className="w-56">
          <TrackMenuItems
            track={track}
            isQueueContext={isQueueContext}
            components={{
              MenuItem: ContextMenuItem,
              MenuSub: ContextMenuSub,
              MenuSubTrigger: ContextMenuSubTrigger,
              MenuSubContent: ContextMenuSubContent,
              MenuSeparator: ContextMenuSeparator,
            }}
          />
        </ContextMenuContent>
      </ContextMenu>
    );
  }

  // Mobile: use three-dot button + bottom sheet
  return (
    <View className={`flex-row items-center -mx-2 px-2 rounded-md ${isActive ? 'bg-primary/10' : ''}`}>
      <View className="flex-1">{children}</View>
      <Pressable
        onPress={() => bottomSheetRef.current?.present()}
        className="p-2 active:bg-muted/50 rounded-md"
        hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
      >
        <MoreVertical size={20} color={theme === 'dark' ? '#a1a1aa' : '#71717a'} />
      </Pressable>

      <MenuBottomSheet ref={bottomSheetRef}>
        {/* Like Track */}
        <BottomSheetMenuItem onPress={handleLike}>
          <Heart size={16} color={iconColor} fill={track.favorited ? iconColor : "none"} />
          <Text>{track.favorited ? 'Remove from favorites' : 'Like'}</Text>
        </BottomSheetMenuItem>

        <BottomSheetMenuSeparator />

        {/* Find Similar */}
        <BottomSheetMenuItem onPress={handleFindSimilar}>
          <Sparkles size={16} color={iconColor} />
          <Text>Find similar songs</Text>
        </BottomSheetMenuItem>

        {/* Add to Queue */}
        {showAddToQueue && (
          <BottomSheetMenuItem onPress={handleAddToQueue}>
            <Plus size={16} color={iconColor} />
            <Text>Add to queue</Text>
          </BottomSheetMenuItem>
        )}

        {/* Remove from Queue */}
        {showRemoveFromQueue && (
          <BottomSheetMenuItem onPress={handleRemoveFromQueue}>
            <Trash2 size={16} color={iconColor} />
            <Text>Remove from queue</Text>
          </BottomSheetMenuItem>
        )}

        <BottomSheetMenuSeparator />

        {/* Go to Album */}
        {track.album?.id && (
          <BottomSheetMenuItem onPress={handleGoToAlbum}>
            <Disc3 size={16} color={iconColor} />
            <Text>Go to album</Text>
          </BottomSheetMenuItem>
        )}

        {/* Artists - flattened for mobile */}
        {track.artists && track.artists.length > 0 && (
          <>
            <BottomSheetMenuSeparator />
            {track.artists.length === 1 ? (
              <BottomSheetMenuItem onPress={() => handleGoToArtist(track.artists[0].id)}>
                <User size={16} color={iconColor} />
                <Text>Go to artist</Text>
              </BottomSheetMenuItem>
            ) : (
              track.artists.map((artist) => (
                <BottomSheetMenuItem key={artist.id} onPress={() => handleGoToArtist(artist.id)}>
                  <User size={16} color={iconColor} />
                  <Text>{artist.name} ({artist.role})</Text>
                </BottomSheetMenuItem>
              ))
            )}
          </>
        )}
      </MenuBottomSheet>
    </View>
  );
}
