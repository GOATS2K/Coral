import { useEffect } from 'react';
import { AlbumRecommendationDto } from '@/lib/client/schemas';
import { useQueryClient } from '@tanstack/react-query';
import { recommendationsForAlbumQuery } from '@/lib/client/components';

/**
 * Hook to eagerly preload recommendations for all visible albums
 * This improves perceived performance by ensuring recommendations
 * are already cached when the user navigates to any album
 */
export function useRecommendationPreload(recommendations: AlbumRecommendationDto[] | undefined) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!recommendations || recommendations.length === 0) return;

    // Preload recommendations for all visible albums
    recommendations.forEach((rec) => {
      const albumId = rec.album.id;

      // Use the same query construction as the generated hook
      const query = recommendationsForAlbumQuery({
        pathParams: { albumId }
      });

      // Check if data is already cached
      const cachedData = queryClient.getQueryData(query.queryKey);

      if (!cachedData) {
        // Prefetch if not already cached
        queryClient.prefetchQuery({
          queryKey: query.queryKey,
          queryFn: query.queryFn,
          // Cache for 2 hours to match server-side cache
          staleTime: 2 * 60 * 60 * 1000,
          gcTime: 2 * 60 * 60 * 1000,
        }).catch((error) => {
          console.error(`[Preload] Failed to prefetch recommendations for album ${albumId}:`, error);
        });
      }
    });
  }, [recommendations, queryClient]);
}