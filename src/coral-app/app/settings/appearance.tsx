import { ScrollView, View } from 'react-native';
import { Stack } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AppearanceSettings } from '@/components/settings/appearance-settings';

export default function AppearanceSettingsPage() {
  const insets = useSafeAreaInsets();

  return (
    <>
      <Stack.Screen
        options={{
          headerShown: true,
          title: 'Appearance',
          headerBackTitle: 'Settings',
        }}
      />
      <ScrollView
        className="flex-1 bg-background"
        contentContainerStyle={{ paddingBottom: insets.bottom + 16 }}
      >
        <View className="p-4">
          <AppearanceSettings />
        </View>
      </ScrollView>
    </>
  );
}
