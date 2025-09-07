import getPrefferedColor from '@/components/util/getPreferredColor';
import '@/global.css';

import { NAV_THEME } from '@/lib/theme';
import { ThemeProvider } from '@react-navigation/native';
import { PortalHost } from '@rn-primitives/portal';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useColorScheme } from 'nativewind';
import { useEffect, useState } from 'react';
import { Appearance, Platform } from 'react-native';

export {
  // Catch any errors thrown by the Layout component.
  ErrorBoundary,
} from 'expo-router';

const queryClient = new QueryClient();

export default function RootLayout() {
  const { setColorScheme } = useColorScheme();
  const [preferredColor, setPrefferedColor] = useState<'light' | 'dark'>(getPrefferedColor());

  // wire up event listeners for platform changes
  useEffect(() => {
    if (Platform.OS === 'web') {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      mediaQuery.addEventListener('change', (e: MediaQueryListEvent) => {
        setPrefferedColor(e.matches ? 'dark' : 'light');
      });
    } else {
      Appearance.addChangeListener((ch) => {
        setPrefferedColor(ch.colorScheme ?? 'light');
      });
    }
  }, []);

  // get preferred color for first run and use preferred color set by event listners
  // in subsequent runs
  useEffect(() => {
    setColorScheme(preferredColor);
  }, [preferredColor]);

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider value={NAV_THEME[preferredColor ?? 'dark']}>
        <StatusBar style={preferredColor === 'dark' ? 'light' : 'dark'} />
        <Stack />
        <PortalHost />
      </ThemeProvider>
    </QueryClientProvider>
  );
}
