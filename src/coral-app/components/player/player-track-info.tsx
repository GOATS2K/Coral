import { View, Image, Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Link, useRouter } from 'expo-router';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { getArtworkUrl } from '@/lib/player/player-format-utils';
import { PlaybackInitializer, PlaybackSource } from '@/lib/state';
import { MoreVertical } from 'lucide-react-native';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { TrackMenuItems } from '@/components/menu-items/track-menu-items';

interface PlayerTrackInfoProps {
  track: SimpleTrackDto;
  initializer?: PlaybackInitializer | null;
}

export function PlayerTrackInfo({ track, initializer }: PlayerTrackInfoProps) {
  const artworkUrl = getArtworkUrl(track.album?.id);
  const mainArtists = track.artists.filter(a => a.role === 'Main');
  const router = useRouter();
  const theme = useAtomValue(themeAtom);

  const handleTrackTitleClick = () => {
    if (!initializer) return;

    switch (initializer.source) {
      case PlaybackSource.Album:
        router.push(`/albums/${initializer.id}`);
        break;
      case PlaybackSource.Search:
        router.push({ pathname: '/search', params: { q: initializer.id } });
        break;
      case PlaybackSource.Favorites:
      case PlaybackSource.Home:
        router.push('/');
        break;
    }
  };

  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

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
        <Pressable onPress={handleTrackTitleClick} disabled={!initializer} className={initializer ? "web:cursor-pointer" : ""}>
          <Text className="text-foreground font-medium select-none web:hover:underline" numberOfLines={1}>
            {track.title}
          </Text>
        </Pressable>
        <View className="flex-row flex-wrap">
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

      {/* Track Menu */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Pressable className="p-2 web:hover:bg-muted/50 rounded active:bg-muted/70">
            <MoreVertical size={20} color={iconColor} />
          </Pressable>
        </DropdownMenuTrigger>
        <DropdownMenuContent className="w-56">
          <TrackMenuItems
            track={track}
            components={{
              MenuItem: DropdownMenuItem,
              MenuSub: DropdownMenuSub,
              MenuSubTrigger: DropdownMenuSubTrigger,
              MenuSubContent: DropdownMenuSubContent,
              MenuSeparator: DropdownMenuSeparator,
            }}
          />
        </DropdownMenuContent>
      </DropdownMenu>
    </View>
  );
}
