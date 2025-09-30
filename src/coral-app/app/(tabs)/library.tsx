import { View } from 'react-native';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Text } from '@/components/ui/text';
import { Stack } from 'expo-router';
import AlbumsTab from './library/albums';
import ArtistsTab from './library/artists';
import TracksTab from './library/tracks';
import { useState } from 'react';

export default function LibraryScreen() {
  const [value, setValue] = useState('albums');

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 bg-background">
        <Tabs value={value} onValueChange={setValue} className="flex-1">
          <View className="mt-4 px-2">
            <TabsList>
              <TabsTrigger value="albums">
                <Text>Albums</Text>
              </TabsTrigger>
              <TabsTrigger value="artists">
                <Text>Artists</Text>
              </TabsTrigger>
              <TabsTrigger value="tracks">
                <Text>Tracks</Text>
              </TabsTrigger>
            </TabsList>
          </View>

          <TabsContent value="albums" className="flex-1">
            <AlbumsTab />
          </TabsContent>

          <TabsContent value="artists" className="flex-1">
            <ArtistsTab />
          </TabsContent>

          <TabsContent value="tracks" className="flex-1">
            <TracksTab />
          </TabsContent>
        </Tabs>
      </View>
    </>
  );
}
