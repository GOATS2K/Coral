import { View, Pressable } from 'react-native';
import { ChevronRightIcon, LucideIcon } from 'lucide-react-native';
import { useAtomValue } from 'jotai';

import { Text } from '@/components/ui/text';
import { Icon } from '@/components/ui/icon';
import { themeAtom } from '@/lib/state';

interface SettingItemProps {
  icon?: LucideIcon;
  label: string;
  value?: string;
  onPress?: () => void;
  showChevron?: boolean;
}

export function SettingItem({ icon, label, value, onPress, showChevron = true }: SettingItemProps) {
  const theme = useAtomValue(themeAtom);
  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

  return (
    <Pressable
      onPress={onPress}
      disabled={!onPress}
      className="flex-row items-center px-4 py-3 active:bg-accent/50 border-b border-border last:border-b-0"
    >
      {icon && (
        <View className="mr-3">
          <Icon as={icon} className="size-5" color={iconColor} />
        </View>
      )}
      <View className="flex-1">
        <Text className="text-base">{label}</Text>
      </View>
      {value && (
        <Text className="text-muted-foreground mr-2">{value}</Text>
      )}
      {onPress && showChevron && (
        <Icon as={ChevronRightIcon} className="size-5 text-muted-foreground" />
      )}
    </Pressable>
  );
}
