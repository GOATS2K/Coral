import { ColorValue, Image, ScrollView, View } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { THEME } from '@/lib/theme';
import { ThemeToggle } from '@/components/util/theme-toggle';
import { useColorScheme } from 'nativewind';
import { useAlbum } from '@/lib/client/components';
import { Text } from '@/components/ui/text';
import { AlbumDto, SimpleTrackDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';
import { LinearGradient } from 'expo-linear-gradient';

const SCREEN_OPTIONS = {
    headerShown: false
};

interface AlbumHeaderCardProps {
  album: AlbumDto;
  gradientColors: string[];
}

function AlbumHeaderCard({ album, gradientColors }: AlbumHeaderCardProps) {
  // Get the medium artwork URL, fallback to small if not available
  const artworkPath = album.artworks?.medium ?? "";
  const artworkUrl = artworkPath ? `${baseUrl}${artworkPath}` : null;
  
  console.log(artworkUrl);

  // Format artists names
  const artistNames = album.artists.length > 4 
    ? 'Various Artists'
    : album.artists.map(artist => artist.name).join(', ');
  
  
  return (
      <LinearGradient
        colors={[...gradientColors, 'rgba(0,0,0,0.4)', 'rgba(0,0,0,0.6)', 'transparent'] as ColorValue[]}
        locations={[0, 0.6, 0.8, 1]}
        start={{ x: 0, y: 0 }}
        end={{ x: 0, y: 1 }}
        className="flex-col sm:flex-row gap-4 pt-8 sm:pt-4 p-4 items-center sm:items-start sm:h-[230px] h-[300px]"
      >
      {/* Album Cover */}
      <View className="mx-auto sm:mx-0">
        <View style={{ width: 200, height: 200 }}>
          {artworkUrl ? (
            <Image 
              source={{ uri: artworkUrl }} 
              style={{ width: 200, height: 200, borderRadius: 8 }}
              resizeMode="cover"
            />
          ) : (
            <View style={{ width: 200, height: 200, borderRadius: 8, backgroundColor: '#f3f4f6', justifyContent: 'center', alignItems: 'center' }}>
              <Text variant="muted">No Cover</Text>
            </View>
          )}
        </View>
      </View>
      
      {/* Album Info */}
      <View className="flex-1 flex-col justify-between items-start sm:items-start">
        <View className="gap-2">
          {/* Album Title */}
          <Text variant="h4" className="text-white font-bold text-center sm:text-left">
            {album.name}
          </Text>
          
          {/* Artists */}
          <Text variant="small" className="text-white text-center sm:text-left">
            {artistNames}
          </Text>
        </View>

        {/* Release Year, Track Count, and Genres - Aligned with artwork bottom: 200px + padding offset */}
        <View className="mt-3 mb-6">
          {(() => {
            const trackCount = album.tracks?.length || 0;
            const year = album.releaseYear;
            
            // Collect unique genres from all tracks
            const allGenres = album.tracks && album.tracks.length > 0
              ? album.tracks
                  .flatMap(track => track.genre || [])
                  .map(genre => genre.name)
                  .filter((name, index, array) => array.indexOf(name) === index) // Remove duplicates
                  .slice(0, 3) // Limit to 3 genres max
              : [];
            
            const infoParts = [];
            if (year) infoParts.push(year);
            if (trackCount > 0) infoParts.push(`${trackCount} tracks`);
            if (allGenres.length > 0) infoParts.push(allGenres.join(', '));
            
            return infoParts.length > 0 ? (
              <Text variant="small" className="text-white opacity-80 text-center sm:text-left">
                {infoParts.join(' • ')}
              </Text>
            ) : null;
          })()}
        </View>
      </View>
    </LinearGradient>
  );
}

interface TrackListingProps {
  tracks: SimpleTrackDto[];
}

function TrackListing({ tracks }: TrackListingProps) {
  // Group tracks by disc number
  const tracksByDisc = tracks.reduce((acc, track, index) => {
    const discNumber = track.discNumber || 1;
    if (!acc[discNumber]) {
      acc[discNumber] = [];
    }
    acc[discNumber].push({ ...track, originalIndex: index });
    return acc;
  }, {} as Record<number, (SimpleTrackDto & { originalIndex: number })[]>);

  // Format duration in MM:SS
  const formatDuration = (seconds: number) => {
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = Math.floor(seconds % 60);
    return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
  };

  // Update track numbering within each disc
  const updateDiscTrackNumbers = () => {
    Object.keys(tracksByDisc).forEach(discNumber => {
      tracksByDisc[parseInt(discNumber)].forEach((track, index) => {
        track.trackNumber = index + 1;
      });
    });
  };

  updateDiscTrackNumbers();

  return (
    <View className="mx-2 pl-4 mt-8 pb-20">
      <View className="gap-2">
        {Object.entries(tracksByDisc).map(([discNumber, discTracks]) => (
          <View key={discNumber}>
            {discTracks.map((track) => (
              <View key={track.id} className="flex-row py-2 items-center">
                {/* Track Number */}
                <Text variant="small" className="text-muted-foreground w-12">
                  {Object.keys(tracksByDisc).length > 1 
                    ? `${discNumber}.${track.trackNumber.toString().padStart(2, '0')}`
                    : track.trackNumber.toString().padStart(2, '0')
                  }
                </Text>
                
                {/* Track Info Stack */}
                <View className="flex-1 ml-1">
                  <Text variant="default" className="text-foreground">
                    {track.title}
                  </Text>
                  <Text variant="small" className="text-muted-foreground mt-1">
                    {track.artists.filter(artist => artist.role === 'Main').map(artist => artist.name).join(', ')}
                  </Text>
                </View>
                
                {/* Duration - Hidden on small screens */}
                <Text variant="small" className="text-muted-foreground hidden sm:block w-16">
                  {formatDuration(track.durationInSeconds)}
                </Text>
              </View>
            ))}
          </View>
        ))}
      </View>
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
  
  const gradientColors = data?.artworks?.colors?.length > 0 
    ? data.artworks.colors.map(reduceColorIntensity)
    : ['#6366f1', '#8b5cf6'].map(reduceColorIntensity);

  // Create faded gradient that extends across page
  const extendedGradientColors = [...gradientColors, 'rgba(0,0,0,0.1)', 'transparent'] as ColorValue[];

  return (
    <>
      <Stack.Screen options={SCREEN_OPTIONS} />
      <View className="flex-1 bg-background">
        <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
          <AlbumHeaderCard album={data} gradientColors={gradientColors} />
          <TrackListing tracks={data.tracks} />
        </ScrollView>
      </View>
    </>
  );
}
