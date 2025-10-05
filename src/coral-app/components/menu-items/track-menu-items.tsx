import { ComponentType } from 'react';
import { Heart, Sparkles, Plus, User, Trash2, Disc3 } from 'lucide-react-native';
import { Text } from '@/components/ui/text';
import { Icon } from '@/components/ui/icon';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { useToggleFavoriteTrack } from '@/lib/hooks/use-toggle-favorite-track';
import { useRouter } from 'expo-router';
import { useSetAtom, useAtomValue } from 'jotai';
import { playerStateAtom } from '@/lib/state';
import { useToast } from '@/lib/hooks/use-toast';
import { addToQueue, findSimilarAndAddToQueue } from '@/lib/player/player-queue-utils';

interface MenuComponents {
  MenuItem: ComponentType<any>;
  MenuSub: ComponentType<any>;
  MenuSubTrigger: ComponentType<any>;
  MenuSubContent: ComponentType<any>;
  MenuSeparator: ComponentType<any>;
}

interface TrackMenuItemsProps {
  track: SimpleTrackDto;
  components: MenuComponents;
  isQueueContext?: boolean;
}

export function TrackMenuItems({ track, components, isQueueContext = false }: TrackMenuItemsProps) {
  const { MenuItem, MenuSub, MenuSubTrigger, MenuSubContent, MenuSeparator } = components;
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
  };

  const handleFindSimilar = async () => {
    await findSimilarAndAddToQueue(track.id, setState, showToast);
  };

  const handleAddToQueue = () => {
    addToQueue(setState, track);
    showToast(`Added "${track.title}" to queue`);
  };

  const handleRemoveFromQueue = () => {
    setState(state => ({
      ...state,
      queue: state.queue.filter(t => t.id !== track.id)
    }));
    showToast(`Removed "${track.title}" from queue`);
  };

  const handleGoToAlbum = () => {
    if (track.album?.id) {
      router.push(`/albums/${track.album.id}`);
    }
  };

  const handleGoToArtist = (artistId: string) => {
    router.push(`/artists/${artistId}`);
  };

  return (
    <>
      {/* Like Track */}
      <MenuItem onPress={handleLike}>
        <Icon as={Heart} size={14} className="text-foreground" fill={track.favorited ? "currentColor" : "none"} />
        <Text>{track.favorited ? 'Remove from favorites' : 'Like'}</Text>
      </MenuItem>

      <MenuSeparator />

      {/* Find Similar */}
      <MenuItem onPress={handleFindSimilar}>
        <Icon as={Sparkles} size={14} className="text-foreground" />
        <Text>Find similar songs</Text>
      </MenuItem>

      {/* Add to Queue */}
      {showAddToQueue && (
        <MenuItem onPress={handleAddToQueue}>
          <Icon as={Plus} size={14} className="text-foreground" />
          <Text>Add to queue</Text>
        </MenuItem>
      )}

      {/* Remove from Queue */}
      {showRemoveFromQueue && (
        <MenuItem onPress={handleRemoveFromQueue}>
          <Icon as={Trash2} size={14} className="text-foreground" />
          <Text>Remove from queue</Text>
        </MenuItem>
      )}

      <MenuSeparator />

      {/* Go to Album */}
      {track.album?.id && (
        <MenuItem onPress={handleGoToAlbum}>
          <Icon as={Disc3} size={14} className="text-foreground" />
          <Text>Go to album</Text>
        </MenuItem>
      )}

      {/* Artists */}
      {track.artists && track.artists.length > 0 && (
        <MenuSub>
          <MenuSubTrigger>
            <Icon as={User} size={14} className="text-foreground" />
            <Text>Artists</Text>
          </MenuSubTrigger>
          <MenuSubContent>
            {track.artists.map((artist) => (
              <MenuItem key={artist.id} onPress={() => handleGoToArtist(artist.id)}>
                <Text>{artist.name} ({artist.role})</Text>
              </MenuItem>
            ))}
          </MenuSubContent>
        </MenuSub>
      )}
    </>
  );
}
