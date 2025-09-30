import { View } from 'react-native';
import { Text } from '@/components/ui/text';

export default function TracksScreen() {
  return (
    <View className="flex-1 items-center justify-center bg-background">
      <Text variant="h3">Tracks</Text>
      <Text className="text-muted-foreground mt-2">Coming soon...</Text>
    </View>
  );
}
