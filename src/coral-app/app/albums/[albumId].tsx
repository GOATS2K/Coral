import { Text, View } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { THEME } from '@/lib/theme';
import { ThemeToggle } from '@/components/util/theme-toggle';
import { useColorScheme } from 'nativewind';
import { useAlbum } from '@/lib/client/components';

const SCREEN_OPTIONS = {
  light: {
    title: 'Albums',
    headerTransparent: true,
    headerShadowVisible: true,
    headerStyle: { backgroundColor: THEME.light.background },
    headerRight: () => <ThemeToggle />,
  },
  dark: {
    title: 'Albums',
    headerTransparent: true,
    headerShadowVisible: true,
    headerStyle: { backgroundColor: THEME.dark.background },
    headerRight: () => <ThemeToggle />,
  },
};

export default function Screen() {
  const { colorScheme } = useColorScheme();
  const { albumId } = useLocalSearchParams();
  const { data, error } = useAlbum({
    pathParams: {
      albumId: albumId as string,
    },
  });
  console.log(data);
  console.log(error);

  return (
    <>
      <Stack.Screen options={SCREEN_OPTIONS[colorScheme ?? 'light']} />
      <View className="flex-1 items-center justify-center gap-8 p-4">
        <Text className="dark:text-white">Got album: {data?.name}</Text>
      </View>
    </>
  );
}
