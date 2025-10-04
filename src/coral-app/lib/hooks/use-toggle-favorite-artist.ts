import { useFavoriteArtist, useRemoveFavoriteArtist } from '@/lib/client/components';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/lib/hooks/use-toast';
import type { SimpleArtistDto } from '@/lib/client/schemas';

interface UseToggleFavoriteArtistOptions {
  showToast?: boolean;
}

export function useToggleFavoriteArtist(options: UseToggleFavoriteArtistOptions = {}) {
  const { showToast: showToastOption = false } = options;
  const queryClient = useQueryClient();
  const favoriteMutation = useFavoriteArtist();
  const removeFavoriteMutation = useRemoveFavoriteArtist();
  const { showToast } = useToast();

  const toggleFavorite = async (artist: SimpleArtistDto) => {
    try {
      if (artist.favorited) {
        await removeFavoriteMutation.mutateAsync({
          pathParams: { artistId: artist.id },
        });
        if (showToastOption) {
          showToast(`Removed "${artist.name}" from favorites`);
        }
      } else {
        await favoriteMutation.mutateAsync({
          pathParams: { artistId: artist.id },
        });
        if (showToastOption) {
          showToast(`Liked "${artist.name}"`);
        }
      }

      await queryClient.invalidateQueries();
    } catch (error) {
      if (showToastOption) {
        showToast(artist.favorited ? 'Failed to remove favorite' : 'Failed to like artist');
      }
      console.error('Error toggling favorite artist:', error);
    }
  };

  return { toggleFavorite };
}
