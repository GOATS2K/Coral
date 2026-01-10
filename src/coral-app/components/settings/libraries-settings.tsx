import { View } from 'react-native';
import { FolderIcon } from 'lucide-react-native';
import { useAtomValue } from 'jotai';

import { Text } from '@/components/ui/text';
import { Icon } from '@/components/ui/icon';
import { themeAtom } from '@/lib/state';

export function LibrariesSettings() {
  const theme = useAtomValue(themeAtom);
  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

  return (
    <View className="gap-6">
      {/* Placeholder for library management */}
      <View className="bg-card rounded-lg p-8 items-center justify-center">
        <Icon as={FolderIcon} className="size-12 mb-4" color={iconColor} />
        <Text className="text-lg font-medium mb-2">Music Libraries</Text>
        <Text className="text-muted-foreground text-center">
          Library management coming soon.{'\n'}
          You'll be able to add, remove, and scan music libraries here.
        </Text>
      </View>
    </View>
  );
}
