import { View, Image, Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Link } from 'expo-router';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { getArtworkUrl } from '@/lib/player/player-format-utils';

interface PlayerTrackInfoProps {
  track: SimpleTrackDto;
}

export function PlayerTrackInfo({ track }: PlayerTrackInfoProps) {
  const artworkUrl = getArtworkUrl(track.album?.id);
  const mainArtists = track.artists.filter(a => a.role === 'Main');

  return (
    <View className="flex-row items-center gap-3 flex-1 min-w-0">
      <Tooltip delayDuration={200}>
        <Link href={`/albums/${track.album?.id}`} asChild>
          <TooltipTrigger asChild>
            <Pressable>
              {artworkUrl ? (
                <Image
                  source={{ uri: artworkUrl }}
                  className="w-14 h-14 rounded"
                  resizeMode="cover"
                />
              ) : (
                <View className="w-14 h-14 rounded bg-muted" />
              )}
            </Pressable>
          </TooltipTrigger>
        </Link>
        <TooltipContent>
          <Text>Go to Album</Text>
        </TooltipContent>
      </Tooltip>
      <View className="flex-1 min-w-0">
        <Text className="text-foreground font-medium select-none" numberOfLines={1}>
          {track.title}
        </Text>
        <View className="flex-row flex-wrap gap-1">
          {mainArtists.map((artist, index) => (
            <View key={artist.id} className="flex-row items-center">
              <Link href={`/artists/${artist.id}`} asChild>
                <Pressable>
                  <Text className="text-muted-foreground text-sm select-none web:hover:underline">
                    {artist.name}
                  </Text>
                </Pressable>
              </Link>
              {index < mainArtists.length - 1 && (
                <Text className="text-muted-foreground text-sm select-none">, </Text>
              )}
            </View>
          ))}
        </View>
      </View>
    </View>
  );
}
