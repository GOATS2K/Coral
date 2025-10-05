import { Tabs } from 'expo-router';
import { Platform, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { HomeIcon, SearchIcon, LibraryIcon } from 'lucide-react-native';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';

export default function TabsLayout() {
  const theme = useAtomValue(themeAtom);
  const insets = useSafeAreaInsets();

  return (
    <View className="flex-1" style={{ paddingTop: Platform.OS === 'web' ? 0 : insets.top }}>
      <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: theme === 'dark' ? '#fff' : '#000',
        tabBarInactiveTintColor: theme === 'dark' ? '#71717a' : '#a1a1aa',
        tabBarStyle: Platform.OS === 'web' ? { display: 'none' } : {
          backgroundColor: theme === 'dark' ? '#09090b' : '#ffffff',
          borderTopColor: theme === 'dark' ? '#27272a' : '#e4e4e7',
        },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: 'Home',
          tabBarIcon: ({ color, size }) => <HomeIcon color={color} size={size} />,
        }}
      />
      <Tabs.Screen
        name="search"
        options={{
          title: 'Search',
          tabBarIcon: ({ color, size }) => <SearchIcon color={color} size={size} />,
        }}
      />
      <Tabs.Screen
        name="library/albums"
        options={{
          title: 'Library',
          tabBarIcon: ({ color, size }) => <LibraryIcon color={color} size={size} />,
          href: '/library/albums',
        }}
      />
      </Tabs>
    </View>
  );
}
