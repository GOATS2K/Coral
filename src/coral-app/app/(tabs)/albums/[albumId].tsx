import { ColorValue, Image, ScrollView, View } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useAlbum } from '@/lib/client/components';
import { Text } from '@/components/ui/text';
import { AlbumDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';
import { LinearGradient } from 'expo-linear-gradient';
import { BlurView } from 'expo-blur';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';
import { TrackListing } from '@/components/track-listing';

const SCREEN_OPTIONS = {
    headerShown: false
};

interface AlbumHeaderCardProps {
  album: AlbumDto;
  gradientColors: string[];
  gradientLocations: number[];
  backgroundColor: string;
}

function AlbumHeaderCard({ album, gradientColors, gradientLocations, backgroundColor }: AlbumHeaderCardProps) {
  // Get the medium artwork URL, fallback to small if not available
  const artworkPath = album.artworks?.medium ?? "";
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;

  // Format artists names
  const artistNames = album.artists.length > 4
    ? 'Various Artists'
    : album.artists.map(artist => artist.name).join(', ');

  // Collect metadata
  const trackCount = album.tracks?.length || 0;
  const year = album.releaseYear;

  // Collect unique genres from all tracks
  const allGenres = album.tracks && album.tracks.length > 0
    ? album.tracks
        .flatMap(track => track.genre || [])
        .map(genre => genre.name)
        .filter((name, index, array) => array.indexOf(name) === index)
        .slice(0, 3)
    : [];

  const infoParts = [];
  if (year) infoParts.push(year);
  if (trackCount > 0) infoParts.push(`${trackCount} tracks`);
  if (allGenres.length > 0) infoParts.push(allGenres.join(', '));

  return (
    <View className="h-[350px] sm:h-[250px] overflow-hidden">
      <LinearGradient
        colors={gradientColors as ColorValue[]}
        locations={gradientLocations}
        className="absolute inset-0"
      />
      <BlurView
        intensity={60}
        tint="dark"
        className="flex-1 flex justify-center"
      >
        <View className="flex-col sm:flex-row gap-4 px-4 items-center sm:items-start">
          {/* Album Cover */}
          <View className="mx-auto sm:mx-0 w-[200px] h-[200px]">
            {artworkUrl ? (
              <Image
                source={{ uri: artworkUrl }}
                className="w-full h-full rounded-lg"
                resizeMode="cover"
              />
            ) : (
              <View className="w-full h-full rounded-lg bg-muted items-center justify-center">
                <Text variant="muted" className="text-white/70">No Cover</Text>
              </View>
            )}
          </View>

          {/* Album Info */}
          <View className="flex-1 flex-col justify-center items-center sm:items-start">
            <View className="gap-2">
              {/* Album Title */}
              <Text variant="h4" className="text-white font-bold text-center sm:text-left drop-shadow-lg">
                {album.name}
              </Text>

              {/* Artists */}
              <Text variant="small" className="text-white text-center sm:text-left drop-shadow-md">
                {artistNames}
              </Text>
            </View>

            {/* Metadata */}
            {infoParts.length > 0 && (
              <View className="mt-3">
                <Text variant="small" className="text-white/90 text-center sm:text-left drop-shadow-md">
                  {infoParts.join(' • ')}
                </Text>
              </View>
            )}
          </View>
        </View>
      </BlurView>
    </View>
  );
}

export default function Screen() {
  const { albumId } = useLocalSearchParams();
  const { data, error } = useAlbum({
    pathParams: {
      albumId: albumId as string,
    },
  });
  const theme = useAtomValue(themeAtom);

  if (error) {
    return (
      <>
        <Stack.Screen options={SCREEN_OPTIONS} />
        <View className="flex-1 items-center justify-center gap-8 p-4">
          <Text className="text-destructive">Error loading album</Text>
        </View>
      </>
    );
  }

  if (!data) {
    return (
      <>
        <Stack.Screen options={SCREEN_OPTIONS} />
        <View className="flex-1 items-center justify-center gap-8 p-4">
          <Text className="text-muted-foreground">Loading...</Text>
        </View>
      </>
    );
  }

  // Get colors from artwork for gradient with reduced intensity
  const reduceColorIntensity = (color: string) => {
    // Add transparency to reduce intensity
    return color.includes('rgba') ? color : `${color}70`;
  };

  const artworkColors = data?.artworks?.colors?.length > 0
    ? data.artworks.colors.map(reduceColorIntensity)
    : ['#6366f1', '#8b5cf6'].map(reduceColorIntensity);

  // Get background color based on theme
  const backgroundColor = theme === 'dark' ? 'hsl(0, 0%, 3.9%)' : 'hsl(0, 0%, 100%)';

  // Create smooth gradient with more stops to reduce banding
  const createSmoothGradient = (colors: string[], bgColor: string) => {
    const gradientStops: string[] = [];
    const locations: number[] = [];

    // Start with first color at top
    gradientStops.push(colors[0]);
    locations.push(0);

    // If multiple artwork colors, space them out in the top portion
    if (colors.length > 1) {
      for (let i = 1; i < colors.length; i++) {
        const position = 0.15 + (i / colors.length) * 0.15;
        gradientStops.push(colors[i]);
        locations.push(position);
      }
    }

    // Add many intermediate stops for ultra-smooth transition to background
    const lastColor = colors[colors.length - 1];
    const startPos = colors.length > 1 ? 0.3 : 0.15;
    const numIntermediateStops = 8;

    for (let i = 1; i <= numIntermediateStops; i++) {
      const ratio = i / (numIntermediateStops + 1);
      // Use easing function for smoother transition
      const easedRatio = ratio * ratio * (3 - 2 * ratio); // Smoothstep
      gradientStops.push(lastColor);
      locations.push(startPos + easedRatio * (0.85 - startPos));
    }

    // Final transition to background
    gradientStops.push(lastColor);
    locations.push(0.9);

    gradientStops.push(bgColor);
    locations.push(1.0);

    return { colors: gradientStops, locations };
  };

  const { colors: gradientColors, locations: gradientLocations } = createSmoothGradient(artworkColors, backgroundColor);

  return (
    <>
      <Stack.Screen options={SCREEN_OPTIONS} />
      <View className="flex-1 bg-background">
        <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
          <AlbumHeaderCard
            album={data}
            gradientColors={gradientColors}
            gradientLocations={gradientLocations}
            backgroundColor={backgroundColor}
          />
          <TrackListing
            tracks={data.tracks}
            album={data}
            showTrackNumber={true}
            className="px-4 sm:px-6 pb-20 mt-6"
          />
        </ScrollView>
      </View>
    </>
  );
}
