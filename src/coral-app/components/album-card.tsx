import { View, Image, Pressable, Platform } from 'react-native';
import { Text } from '@/components/ui/text';
import { baseUrl } from '@/lib/client/fetcher';
import { Link } from 'expo-router';
import { useState, useMemo, useCallback } from 'react';
import type { SimpleAlbumDto } from '@/lib/client/schemas';
import { PlayIcon, MoreVerticalIcon } from 'lucide-react-native';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuSub, DropdownMenuSubContent, DropdownMenuSubTrigger, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { usePlayerActions } from '@/lib/player/use-player';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
import { AlbumMenuItems } from '@/components/menu-items/album-menu-items';
import { PlaybackSource } from '@/lib/state';

interface AlbumCardProps {
  album: SimpleAlbumDto;
}

export function AlbumCard({ album }: AlbumCardProps) {
  const isWeb = Platform.OS === 'web';
  const { play } = usePlayerActions();
  const artworkSize = isWeb ? 150 : 180;
  const artworkPath = album.artworks?.medium ?? album.artworks?.small ?? '';
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;
  const [isHovered, setIsHovered] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const artistNames = useMemo(() => {
    if (album.artists && album.artists.length > 4) {
      return 'Various Artists';
    }
    if (album.artists && album.artists.length > 0) {
      return album.artists.map(a => a.name).join(', ');
    }
    return 'Unknown Artist';
  }, [album.artists]);

  const fetchAlbumTracks = useCallback(async () => {
    const response = await fetch(`${baseUrl}/api/library/albums/${album.id}/tracks`);
    if (!response.ok) throw new Error('Failed to fetch tracks');
    return await response.json();
  }, [album.id]);

  const handlePlayAlbum = useCallback(async (e: any) => {
    e.preventDefault();
    e.stopPropagation();

    try {
      const tracks = await fetchAlbumTracks();
      if (tracks && tracks.length > 0) {
        play(tracks, 0, {
          source: PlaybackSource.Album,
          id: album.id,
        });
      }
    } catch (error) {
      console.error('Error playing album:', error);
    }
  }, [fetchAlbumTracks, play, album.id]);

  return (
    <Link href={`/albums/${album.id}`} asChild>
      <Pressable className="p-2" style={{ width: artworkSize + 16 }}>
        <View className="gap-2" style={{ width: artworkSize }}>
          {/* Album Artwork */}
          <View
            className="aspect-square rounded-lg overflow-hidden bg-muted relative"
            style={{ width: artworkSize, height: artworkSize }}
            onPointerEnter={isWeb ? () => setIsHovered(true) : undefined}
            onPointerLeave={isWeb ? () => setIsHovered(false) : undefined}
          >
            {artworkUrl ? (
              <Image
                source={{ uri: artworkUrl }}
                className="w-full h-full"
                resizeMode="cover"
              />
            ) : (
              <MissingAlbumCover size={64} />
            )}

            {/* Hover Overlay (Web only) */}
            {isWeb && isHovered && (
              <View className="absolute inset-0 bg-black/40 items-center justify-center">
                {/* Play Button */}
                <Pressable
                  onPress={handlePlayAlbum}
                  className="bg-white rounded-full p-3 hover:scale-110 transition-transform"
                >
                  <PlayIcon size={32} color="#000" fill="#000" />
                </Pressable>

                {/* Dropdown Menu Button - only render when hovered */}
                <View className="absolute top-2 right-2" pointerEvents="box-none">
                  <DropdownMenu onOpenChange={setIsMenuOpen}>
                    <DropdownMenuTrigger asChild>
                      <Pressable
                        className="bg-black/50 rounded-full p-2 hover:bg-black/70"
                        onPress={(e) => {
                          e.preventDefault();
                          e.stopPropagation();
                        }}
                      >
                        <MoreVerticalIcon size={20} color="#fff" />
                      </Pressable>
                    </DropdownMenuTrigger>
                    {/* Only render menu content when dropdown is open */}
                    {isMenuOpen && (
                      <DropdownMenuContent className="w-48">
                        <AlbumMenuItems
                          album={album}
                          components={{
                            MenuItem: DropdownMenuItem,
                            MenuSub: DropdownMenuSub,
                            MenuSubTrigger: DropdownMenuSubTrigger,
                            MenuSubContent: DropdownMenuSubContent,
                            MenuSeparator: DropdownMenuSeparator,
                          }}
                        />
                      </DropdownMenuContent>
                    )}
                  </DropdownMenu>
                </View>
              </View>
            )}
          </View>

          {/* Album Info */}
          <View className="gap-0.5">
            <Text className="font-semibold text-sm" numberOfLines={2}>
              {album.name}
            </Text>
            <Text className="text-muted-foreground text-xs" numberOfLines={1}>
              {artistNames}
            </Text>
            {album.releaseYear ? (
              <Text className="text-muted-foreground text-xs">
                {album.releaseYear}
              </Text>
            ) : null}
          </View>
        </View>
      </Pressable>
    </Link>
  );
}
