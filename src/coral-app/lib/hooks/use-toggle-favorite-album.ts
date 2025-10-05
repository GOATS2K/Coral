import { useFavoriteAlbum, useRemoveFavoriteAlbum } from '@/lib/client/components';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/lib/hooks/use-toast';
import type { SimpleAlbumDto, AlbumDto } from '@/lib/client/schemas';

interface UseToggleFavoriteAlbumOptions {
  showToast?: boolean;
}

export function useToggleFavoriteAlbum(options: UseToggleFavoriteAlbumOptions = {}) {
  const { showToast: showToastOption = false } = options;
  const queryClient = useQueryClient();
  const favoriteMutation = useFavoriteAlbum();
  const removeFavoriteMutation = useRemoveFavoriteAlbum();
  const { showToast } = useToast();

  const toggleFavorite = async (album: SimpleAlbumDto | AlbumDto) => {
    try {
      if (album.favorited) {
        await removeFavoriteMutation.mutateAsync({
          pathParams: { albumId: album.id },
        });
        if (showToastOption) {
          showToast(`Removed "${album.name}" from favorites`);
        }
      } else {
        await favoriteMutation.mutateAsync({
          pathParams: { albumId: album.id },
        });
        if (showToastOption) {
          showToast(`Liked "${album.name}"`);
        }
      }

      await queryClient.invalidateQueries();
    } catch (error) {
      if (showToastOption) {
        showToast(album.favorited ? 'Failed to remove favorite' : 'Failed to like album');
      }
      console.error('Error toggling favorite album:', error);
    }
  };

  return { toggleFavorite };
}
