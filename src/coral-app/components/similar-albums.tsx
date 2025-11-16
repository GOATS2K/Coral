import { View } from 'react-native';
import { useRecommendationsForAlbum } from '@/lib/client/components';
import { Text } from '@/components/ui/text';
import { UniversalAlbumCard } from '@/components/universal-album-card';
import { useRecommendationPreload } from '@/lib/hooks/use-recommendation-preload';

interface SimilarAlbumsProps {
  albumId: string;
}

export function SimilarAlbums({ albumId }: SimilarAlbumsProps) {
  const { data: recommendations, isLoading } = useRecommendationsForAlbum({
    pathParams: { albumId }
  });

  // Eagerly preload recommendations for all visible albums
  useRecommendationPreload(recommendations);

  // Don't show anything while loading (silent loading)
  if (isLoading) {
    return null;
  }

  if (!recommendations || recommendations.length === 0) {
    return null;
  }

  return (
    <View className="pb-24">
      {/* Title */}
      <View className="px-4 mb-2">
        <Text variant="h5" className="font-semibold">Sonically Similar Albums</Text>
      </View>

      {/* Albums grid */}
      <View className="flex-row flex-wrap px-2">
        {recommendations.slice(0, 10).map((rec) => (
          <UniversalAlbumCard key={rec.album.id} album={rec.album} />
        ))}
      </View>
    </View>
  );
}