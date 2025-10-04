import { Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { Link } from 'expo-router';
import { memo } from 'react';
import type { SimpleArtistDto } from '@/lib/client/schemas';
import { Heart } from 'lucide-react-native';
import { useToggleFavoriteArtist } from '@/lib/hooks/use-toggle-favorite-artist';

interface ArtistCardProps {
  artist: SimpleArtistDto;
}

export const ArtistCard = memo(function ArtistCard({ artist }: ArtistCardProps) {
  const { toggleFavorite } = useToggleFavoriteArtist();

  const handleLikeArtist = async (e: any) => {
    e.preventDefault();
    e.stopPropagation();
    await toggleFavorite(artist);
  };

  return (
    <Link href={`/artists/${artist.id}`} asChild>
      <Pressable className="py-3 px-4 bg-muted/30 rounded-lg web:hover:bg-muted/50 active:bg-muted/60 flex-row items-center justify-between">
        <Text className="font-medium flex-1">{artist.name}</Text>
        <Pressable
          onPress={handleLikeArtist}
          className="ml-2 p-2 web:hover:bg-muted/70 active:bg-muted/80 rounded-full"
        >
          <Heart
            size={20}
            className="text-foreground"
            fill={artist.favorited ? "currentColor" : "none"}
          />
        </Pressable>
      </Pressable>
    </Link>
  );
});
