import getPrefferedColor from '@/components/util/getPreferredColor';
import '@/global.css';

import { NAV_THEME } from '@/lib/theme';
import { ThemeProvider } from '@react-navigation/native';
import { PortalHost } from '@rn-primitives/portal';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useColorScheme } from 'nativewind';
import { useEffect } from 'react';
import { Appearance, Platform } from 'react-native';
import { useAtomValue, useSetAtom } from 'jotai';
import { themeAtom, systemThemeAtom, themePreferenceAtom } from '@/lib/state';
import { WebPlayerBar } from '@/components/player/web-player-bar';
import { PlayerProvider } from '@/lib/player/player-provider';
import AsyncStorage from '@react-native-async-storage/async-storage';

export {
  // Catch any errors thrown by the Layout component.
  ErrorBoundary,
} from 'expo-router';

const queryClient = new QueryClient();

export default function RootLayout() {
  const { colorScheme, setColorScheme } = useColorScheme();
  const resolvedTheme = useAtomValue(themeAtom);
  const setSystemTheme = useSetAtom(systemThemeAtom);
  const setThemePreference = useSetAtom(themePreferenceAtom);

  console.log('[RootLayout] Render - colorScheme:', colorScheme, 'resolvedTheme:', resolvedTheme);

  useEffect(() => {
    // On native platforms, load theme preference from AsyncStorage
    // (On web, it's already loaded synchronously in the atom)
    if (Platform.OS !== 'web') {
      AsyncStorage.getItem('theme-preference').then(stored => {
        if (stored) {
          const preference = JSON.parse(stored);
          console.log('[RootLayout] Init (native) - loaded theme preference from storage:', preference);
          setThemePreference(preference);
        }
      }).catch(e => console.error('[RootLayout] Init (native) - error loading theme preference:', e));
    }

    // Initialize system theme
    const systemTheme = getPrefferedColor();
    console.log('[RootLayout] Init - setting system theme to:', systemTheme);
    setSystemTheme(systemTheme);

    // wire up event listeners for platform changes
    if (Platform.OS === 'web') {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      const handler = (e: MediaQueryListEvent) => {
        const newTheme = e.matches ? 'dark' : 'light';
        console.log('[RootLayout] Web media query change - setting system theme to:', newTheme);
        setSystemTheme(newTheme);
      };
      mediaQuery.addEventListener('change', handler);
      return () => mediaQuery.removeEventListener('change', handler);
    } else {
      const subscription = Appearance.addChangeListener((ch) => {
        const newTheme = ch.colorScheme === 'dark' ? 'dark' : 'light';
        console.log('[RootLayout] Native appearance change - setting system theme to:', newTheme);
        setSystemTheme(newTheme);
      });
      return () => subscription.remove();
    }
  }, [])

  // Apply resolved theme to NativeWind
  useEffect(() => {
    console.log('[RootLayout] Resolved theme effect - setting colorScheme from', colorScheme, 'to', resolvedTheme);
    setColorScheme(resolvedTheme);
  }, [resolvedTheme]);

  return (
    <QueryClientProvider client={queryClient}>
      <PlayerProvider>
        <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
          <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
          <Stack screenOptions={{ headerShown: false }} />
          <PortalHost />
          {Platform.OS === 'web' && <WebPlayerBar />}
        </ThemeProvider>
      </PlayerProvider>
    </QueryClientProvider>
  );
}
