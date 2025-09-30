import { View } from 'react-native';
import { Stack } from 'expo-router';
import { Text } from '@/components/ui/text';

export default function SearchScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <View className="flex-1 items-center justify-center bg-background">
        <Text variant="h3">Search</Text>
        <Text className="text-muted-foreground mt-2">Coming soon...</Text>
      </View>
    </>
  );
}
