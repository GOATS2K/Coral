import { View } from 'react-native';
import { Text } from '@/components/ui/text';

interface SettingsSectionProps {
  title: string;
  children: React.ReactNode;
}

export function SettingsSection({ title, children }: SettingsSectionProps) {
  return (
    <View className="mb-6">
      <Text className="text-sm font-medium text-muted-foreground uppercase tracking-wide mb-2 px-1">
        {title}
      </Text>
      <View className="bg-card rounded-lg overflow-hidden">
        {children}
      </View>
    </View>
  );
}
