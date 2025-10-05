import { useFavoriteTrack, useRemoveFavoriteTrack } from '@/lib/client/components';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/lib/hooks/use-toast';
import type { SimpleTrackDto } from '@/lib/client/schemas';

interface UseToggleFavoriteTrackOptions {
  showToast?: boolean;
}

export function useToggleFavoriteTrack(options: UseToggleFavoriteTrackOptions = {}) {
  const { showToast: showToastOption = true } = options;
  const queryClient = useQueryClient();
  const favoriteMutation = useFavoriteTrack();
  const removeFavoriteMutation = useRemoveFavoriteTrack();
  const { showToast } = useToast();

  const toggleFavorite = async (track: SimpleTrackDto) => {
    try {
      if (track.favorited) {
        await removeFavoriteMutation.mutateAsync({
          pathParams: { trackId: track.id },
        });
        if (showToastOption) {
          showToast(`Removed "${track.title}" from favorites`);
        }
      } else {
        await favoriteMutation.mutateAsync({
          pathParams: { trackId: track.id },
        });
        if (showToastOption) {
          showToast(`Liked "${track.title}"`);
        }
      }

      await queryClient.invalidateQueries();
    } catch (error) {
      if (showToastOption) {
        showToast(track.favorited ? 'Failed to remove favorite' : 'Failed to like track');
      }
      console.error('Error toggling favorite track:', error);
    }
  };

  return { toggleFavorite };
}
