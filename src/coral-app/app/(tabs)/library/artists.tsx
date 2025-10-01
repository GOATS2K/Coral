import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import { useRouter, usePathname } from 'expo-router';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';

export default function ArtistsScreen() {
  const router = useRouter();
  const pathname = usePathname();

  const handleTabChange = (value: string) => {
    router.push(`/library/${value}`);
  };

  // Determine current tab from pathname
  const currentTab = pathname.split('/').pop() || 'artists';

  return (
    <View className="flex-1 bg-background">
      {/* Tab Navigation */}
      <View className="pt-4 pb-2 px-4 -ml-[3px]">
        <Tabs value={currentTab} onValueChange={handleTabChange} className="flex-1 gap-0">
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
        </Tabs>
      </View>

      <View className="flex-1 items-center justify-center">
        <Text variant="h3">Artists</Text>
        <Text className="text-muted-foreground mt-2">Coming soon...</Text>
      </View>
    </View>
  );
}
