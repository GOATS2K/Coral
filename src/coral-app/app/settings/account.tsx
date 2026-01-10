import { ScrollView, View } from 'react-native';
import { Stack } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AccountSettings } from '@/components/settings/account-settings';

export default function AccountSettingsPage() {
  const insets = useSafeAreaInsets();

  return (
    <>
      <Stack.Screen
        options={{
          headerShown: true,
          title: 'Account',
          headerBackTitle: 'Settings',
        }}
      />
      <ScrollView
        className="flex-1 bg-background"
        contentContainerStyle={{ paddingBottom: insets.bottom + 16 }}
      >
        <View className="p-4">
          <AccountSettings />
        </View>
      </ScrollView>
    </>
  );
}
