import { ScrollView, View, ActivityIndicator, Platform } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useArtist } from '@/lib/client/components';
import { Text } from '@/components/ui/text';
import type { SimpleAlbumDto, AlbumType } from '@/lib/client/schemas';
import { AlbumCard } from '@/components/album-card';

const SCREEN_OPTIONS = {
  headerShown: false
};

// Sort albums by release year descending (newest first), null years at the end
const sortByReleaseYear = (albums: SimpleAlbumDto[]) => {
  return [...albums].sort((a, b) => {
    if (a.releaseYear === null && b.releaseYear === null) return 0;
    if (a.releaseYear === null) return 1;
    if (b.releaseYear === null) return -1;
    return b.releaseYear - a.releaseYear;
  });
};

interface AlbumSectionProps {
  title: string;
  albums: SimpleAlbumDto[];
  showDivider?: boolean;
  dividerLabel?: string;
  albumsAfterDivider?: SimpleAlbumDto[];
}

function AlbumSection({ title, albums, showDivider, dividerLabel, albumsAfterDivider }: AlbumSectionProps) {
  const isWeb = Platform.OS === 'web';
  const numColumns = isWeb ? 6 : 2;

  if (albums.length === 0 && (!albumsAfterDivider || albumsAfterDivider.length === 0)) return null;

  return (
    <View className="mb-8">
      <Text variant="h4" className="mb-4 px-4">{title}</Text>
      <View className="flex-row flex-wrap">
        {albums.map((album) => (
          <View
            key={album.id}
            style={{ width: `${100 / numColumns}%` }}
          >
            <AlbumCard album={album} />
          </View>
        ))}
      </View>

      {showDivider && albumsAfterDivider && albumsAfterDivider.length > 0 && (
        <>
          <View className="my-6 px-4">
            <View className="border-b border-border" />
            {dividerLabel && (
              <Text className="text-muted-foreground text-sm mt-2">{dividerLabel}</Text>
            )}
          </View>
          <View className="flex-row flex-wrap">
            {albumsAfterDivider.map((album) => (
              <View
                key={album.id}
                style={{ width: `${100 / numColumns}%` }}
              >
                <AlbumCard album={album} />
              </View>
            ))}
          </View>
        </>
      )}
    </View>
  );
}

export default function ArtistScreen() {
  const { artistId } = useLocalSearchParams();
  const { data, error, isLoading } = useArtist({
    pathParams: {
      artistId: artistId as string,
    },
  });

  if (error) {
    return (
      <>
        <Stack.Screen options={SCREEN_OPTIONS} />
        <View className="flex-1 items-center justify-center gap-8 p-4 bg-background">
          <Text className="text-destructive">Error loading artist</Text>
        </View>
      </>
    );
  }

  if (isLoading || !data) {
    return (
      <>
        <Stack.Screen options={SCREEN_OPTIONS} />
        <View className="flex-1 items-center justify-center gap-8 p-4 bg-background">
          <ActivityIndicator size="large" />
        </View>
      </>
    );
  }

  // Group and sort albums
  const albums = sortByReleaseYear(
    data.releases.filter((r) => r.type === 'Album')
  );

  const eps = sortByReleaseYear(
    data.releases.filter((r) => r.type === 'EP')
  );

  const miniAlbums = sortByReleaseYear(
    data.releases.filter((r) => r.type === 'MiniAlbum')
  );

  const singles = sortByReleaseYear(
    data.releases.filter((r) => r.type === 'Single')
  );

  const featuredIn = sortByReleaseYear(data.featuredIn);

  const remixerIn = sortByReleaseYear(data.remixerIn);

  // Appears In: combine inCompilation + releases with null type or Compilation type
  const appearsIn = sortByReleaseYear([
    ...data.inCompilation,
    ...data.releases.filter((r) => r.type === null || r.type === 'Compilation')
  ]);

  // Calculate total releases
  const totalReleases = data.releases.length + data.featuredIn.length + data.remixerIn.length + data.inCompilation.length;

  return (
    <>
      <Stack.Screen options={SCREEN_OPTIONS} />
      <View className="flex-1 bg-background">
        <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
          {/* Artist Header */}
          <View className="bg-secondary px-4 pt-8 pb-6 items-start">
            <Text variant="h1" className="font-bold text-secondary-foreground">{data.name}</Text>
            <Text className="text-secondary-foreground/70 mt-2">
              {totalReleases} {totalReleases === 1 ? 'release' : 'releases'}
            </Text>
          </View>

          <View className="mt-6">
            {/* Album Sections */}
            <AlbumSection title="Albums" albums={albums} />
            <AlbumSection
              title="EPs"
              albums={eps}
              showDivider={miniAlbums.length > 0}
              albumsAfterDivider={miniAlbums}
            />
            <AlbumSection title="Singles" albums={singles} />
            <AlbumSection title="Featured In" albums={featuredIn} />
            <AlbumSection title="Remixer In" albums={remixerIn} />
            <AlbumSection title="Appears In" albums={appearsIn} />
          </View>

          {/* Bottom Padding */}
          <View className="pb-20" />
        </ScrollView>
      </View>
    </>
  );
}
