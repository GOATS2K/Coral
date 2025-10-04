import { ComponentType } from 'react';
import { Heart, ListPlus, User } from 'lucide-react-native';
import { Text } from '@/components/ui/text';
import type { SimpleAlbumDto } from '@/lib/client/schemas';
import { useToggleFavoriteAlbum } from '@/lib/hooks/use-toggle-favorite-album';
import { useRouter } from 'expo-router';
import { useSetAtom } from 'jotai';
import { playerStateAtom } from '@/lib/state';
import { addMultipleToQueue } from '@/lib/player/player-queue-utils';
import { baseUrl } from '@/lib/client/fetcher';

interface MenuComponents {
  MenuItem: ComponentType<any>;
  MenuSub: ComponentType<any>;
  MenuSubTrigger: ComponentType<any>;
  MenuSubContent: ComponentType<any>;
  MenuSeparator: ComponentType<any>;
}

interface AlbumMenuItemsProps {
  album: SimpleAlbumDto;
  components: MenuComponents;
}

export function AlbumMenuItems({ album, components }: AlbumMenuItemsProps) {
  const { MenuItem, MenuSub, MenuSubTrigger, MenuSubContent, MenuSeparator } = components;
  const { toggleFavorite } = useToggleFavoriteAlbum();
  const router = useRouter();
  const setState = useSetAtom(playerStateAtom);

  const handleLike = () => {
    toggleFavorite(album);
  };

  const handleAddToQueue = async () => {
    try {
      const response = await fetch(`${baseUrl}/api/library/albums/${album.id}/tracks`);
      if (!response.ok) throw new Error('Failed to fetch tracks');
      const tracks = await response.json();
      if (tracks && tracks.length > 0) {
        addMultipleToQueue(setState, tracks);
      }
    } catch (error) {
      console.error('Error adding to queue:', error);
    }
  };

  const handleGoToArtist = (artistId: string) => {
    router.push(`/artists/${artistId}`);
  };

  return (
    <>
      {/* Like Album */}
      <MenuItem onPress={handleLike}>
        <Heart size={14} className="text-foreground" fill={album.favorited ? "currentColor" : "none"} />
        <Text>{album.favorited ? 'Remove from favorites' : 'Like album'}</Text>
      </MenuItem>

      <MenuSeparator />

      {/* Add to Queue */}
      <MenuItem onPress={handleAddToQueue}>
        <ListPlus size={14} className="text-foreground" />
        <Text>Add to queue</Text>
      </MenuItem>

      <MenuSeparator />

      {/* Artists */}
      {album.artists && album.artists.length > 0 && (
        <MenuSub>
          <MenuSubTrigger>
            <User size={14} className="text-foreground" />
            <Text>Artists</Text>
          </MenuSubTrigger>
          <MenuSubContent>
            {album.artists.map((artist) => (
              <MenuItem key={artist.id} onPress={() => handleGoToArtist(artist.id)}>
                <Text>{artist.name}</Text>
              </MenuItem>
            ))}
          </MenuSubContent>
        </MenuSub>
      )}
    </>
  );
}
