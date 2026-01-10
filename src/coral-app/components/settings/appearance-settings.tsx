import { View, Pressable } from 'react-native';
import { MoonStarIcon, SunIcon, MonitorIcon, CheckIcon } from 'lucide-react-native';
import { useAtom } from 'jotai';
import { useAtomValue } from 'jotai';

import { Text } from '@/components/ui/text';
import { Icon } from '@/components/ui/icon';
import { SettingsSection } from '@/components/settings/settings-section';
import { themePreferenceAtom, themeAtom, type ThemePreference } from '@/lib/state';
import { cn } from '@/lib/utils';

const THEME_OPTIONS: { value: ThemePreference; label: string; icon: typeof SunIcon }[] = [
  { value: 'light', label: 'Light', icon: SunIcon },
  { value: 'dark', label: 'Dark', icon: MoonStarIcon },
  { value: 'system', label: 'System', icon: MonitorIcon },
];

export function AppearanceSettings() {
  const [themePreference, setThemePreference] = useAtom(themePreferenceAtom);
  const theme = useAtomValue(themeAtom);

  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';
  const activeIconColor = theme === 'dark' ? '#fff' : '#000';

  return (
    <View className="gap-6">
      <SettingsSection title="Theme">
        <View className="p-2">
          <View className="flex-row gap-2">
            {THEME_OPTIONS.map((option) => {
              const isActive = themePreference === option.value;
              return (
                <Pressable
                  key={option.value}
                  onPress={() => setThemePreference(option.value)}
                  className={cn(
                    'flex-1 items-center justify-center p-4 rounded-lg border-2',
                    isActive
                      ? 'border-primary bg-primary/10'
                      : 'border-border bg-card hover:bg-accent/50'
                  )}
                >
                  <Icon
                    as={option.icon}
                    className="size-6 mb-2"
                    color={isActive ? activeIconColor : iconColor}
                  />
                  <Text
                    className={cn(
                      'text-sm font-medium',
                      isActive ? 'text-foreground' : 'text-muted-foreground'
                    )}
                  >
                    {option.label}
                  </Text>
                  {isActive && (
                    <View className="absolute top-2 right-2">
                      <Icon as={CheckIcon} className="size-4 text-primary" />
                    </View>
                  )}
                </Pressable>
              );
            })}
          </View>
        </View>
      </SettingsSection>
    </View>
  );
}
