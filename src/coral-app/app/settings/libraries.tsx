import { ScrollView, View } from 'react-native';
import { Stack } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { LibrariesSettings } from '@/components/settings/libraries-settings';

export default function LibrariesSettingsPage() {
  const insets = useSafeAreaInsets();

  return (
    <>
      <Stack.Screen
        options={{
          headerShown: true,
          title: 'Libraries',
          headerBackTitle: 'Settings',
        }}
      />
      <ScrollView
        className="flex-1 bg-background"
        contentContainerStyle={{ paddingBottom: insets.bottom + 16 }}
      >
        <View className="p-4">
          <LibrariesSettings />
        </View>
      </ScrollView>
    </>
  );
}
