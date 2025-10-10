import { Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { Link } from 'expo-router';
import type { SimpleArtistDto } from '@/lib/client/schemas';
import { Heart } from 'lucide-react-native';
import { useToggleFavoriteArtist } from '@/lib/hooks/use-toggle-favorite-artist';
import { Icon } from '@/components/ui/icon';

interface ArtistCardProps {
  artist: SimpleArtistDto;
}

export function ArtistCard({ artist }: ArtistCardProps) {
  const { toggleFavorite } = useToggleFavoriteArtist();

  const handleLikeArtist = async (e: any) => {
    e.preventDefault();
    e.stopPropagation();
    await toggleFavorite(artist);
  };

  return (
    <Link href={`/artists/${artist.id}`} asChild>
      <Pressable className="native:w-full web:w-[calc(50%-0.25rem)] py-2 px-3 web:hover:bg-muted/30 active:bg-muted/50 rounded-lg flex-row items-center justify-between">
        <Text className="font-medium text-sm flex-1">{artist.name}</Text>
        <Pressable
          onPress={handleLikeArtist}
          className="ml-2 p-1.5 web:hover:bg-muted/70 active:bg-muted/80 rounded-full"
        >
          <Icon
            as={Heart}
            size={16}
            className="text-foreground"
            fill={artist.favorited ? "currentColor" : "none"}
          />
        </Pressable>
      </Pressable>
    </Link>
  );
}
