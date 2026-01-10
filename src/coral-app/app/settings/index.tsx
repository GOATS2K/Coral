import { Platform, ScrollView, View, Pressable } from 'react-native';
import { Stack, useRouter } from 'expo-router';
import { useState } from 'react';
import { useAtomValue } from 'jotai';
import { UserIcon, FolderIcon, PaletteIcon } from 'lucide-react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Text } from '@/components/ui/text';
import { SettingsSection } from '@/components/settings/settings-section';
import { SettingItem } from '@/components/settings/setting-item';
import { AccountSettings } from '@/components/settings/account-settings';
import { LibrariesSettings } from '@/components/settings/libraries-settings';
import { AppearanceSettings } from '@/components/settings/appearance-settings';
import { currentUserAtom } from '@/lib/state';
import { cn } from '@/lib/utils';

export default function SettingsPage() {
  if (Platform.OS === 'web') {
    return <WebSettingsPage />;
  }
  return <MobileSettingsPage />;
}

type SettingsSection = 'account' | 'libraries' | 'appearance';

interface NavButtonProps {
  active: boolean;
  onPress: () => void;
  label: string;
}

function NavButton({ active, onPress, label }: NavButtonProps) {
  return (
    <Pressable
      onPress={onPress}
      className={cn(
        'px-4 py-2 rounded-lg',
        active ? 'bg-accent' : 'hover:bg-accent/50'
      )}
    >
      <Text className={cn(active ? 'font-medium' : 'text-muted-foreground')}>
        {label}
      </Text>
    </Pressable>
  );
}

function WebSettingsPage() {
  const insets = useSafeAreaInsets();
  const [activeSection, setActiveSection] = useState<SettingsSection>('account');

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <ScrollView
        className="flex-1 bg-background"
        contentContainerStyle={{ paddingBottom: insets.bottom + 16 }}
      >
        <View className="p-4 pt-6 max-w-2xl">
          <Text variant="h2" className="mb-6">Settings</Text>

          {/* Horizontal nav buttons */}
          <View className="flex-row gap-2 mb-6">
            <NavButton
              active={activeSection === 'account'}
              onPress={() => setActiveSection('account')}
              label="Account"
            />
            <NavButton
              active={activeSection === 'libraries'}
              onPress={() => setActiveSection('libraries')}
              label="Libraries"
            />
            <NavButton
              active={activeSection === 'appearance'}
              onPress={() => setActiveSection('appearance')}
              label="Appearance"
            />
          </View>

          {/* Content panel */}
          <View>
            {activeSection === 'account' && <AccountSettings />}
            {activeSection === 'libraries' && <LibrariesSettings />}
            {activeSection === 'appearance' && <AppearanceSettings />}
          </View>
        </View>
      </ScrollView>
    </>
  );
}

function MobileSettingsPage() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const currentUser = useAtomValue(currentUserAtom);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <ScrollView
        className="flex-1 bg-background"
        contentContainerStyle={{ paddingBottom: insets.bottom + 16 }}
      >
        <View className="p-4 pt-6">
          <Text variant="h2" className="mb-6">Settings</Text>

          <SettingsSection title="">
            <SettingItem
              icon={UserIcon}
              label="Account"
              value={currentUser?.username}
              onPress={() => router.push('/settings/account')}
            />
            <SettingItem
              icon={FolderIcon}
              label="Libraries"
              onPress={() => router.push('/settings/libraries')}
            />
            <SettingItem
              icon={PaletteIcon}
              label="Appearance"
              onPress={() => router.push('/settings/appearance')}
            />
          </SettingsSection>
        </View>
      </ScrollView>
    </>
  );
}
