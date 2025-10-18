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
import { Appearance, Platform, View, ActivityIndicator } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { BottomSheetModalProvider } from '@gorhom/bottom-sheet';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { Provider, useAtomValue, useSetAtom } from 'jotai';
import { themeAtom, systemThemeAtom, themePreferenceAtom } from '@/lib/state';
import { WebPlayerBar } from '@/components/player/web-player-bar';
import { PlayerProvider } from '@/lib/player/player-provider';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { ToastContainer } from '@/components/toast-container';
import { Sidebar } from '@/components/ui/sidebar';
import { Config } from '@/lib/config';
import OnboardingScreen from './onboarding';

export {
  // Catch any errors thrown by the Layout component.
  ErrorBoundary,
} from 'expo-router';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Add logging for debugging
      logger: {
        log: (...args) => console.log('[QueryClient]', ...args),
        warn: (...args) => console.warn('[QueryClient]', ...args),
        error: (...args) => console.error('[QueryClient]', ...args),
      },
    },
  },
});

function AppContent() {
  const { colorScheme, setColorScheme } = useColorScheme();
  const resolvedTheme = useAtomValue(themeAtom);
  const setSystemTheme = useSetAtom(systemThemeAtom);
  const setThemePreference = useSetAtom(themePreferenceAtom);
  const [isReady, setIsReady] = useState(false);
  const [needsOnboarding, setNeedsOnboarding] = useState(false);

  // Check configuration on app startup
  useEffect(() => {
    async function checkInitialConfig() {
      try {
        console.info('[RootLayout] Checking initial config...');
        const isFirstRun = await Config.isFirstRun();
        const backendUrl = await Config.getBackendUrl();

        console.info('[RootLayout] First run:', isFirstRun, 'Backend URL:', backendUrl);

        if (isFirstRun || !backendUrl) {
          console.info('[RootLayout] Onboarding needed');
          setNeedsOnboarding(true);
        } else {
          console.info('[RootLayout] Config exists, no onboarding needed');
          setNeedsOnboarding(false);
        }
      } catch (error) {
        console.error('[RootLayout] Error checking initial config:', error);
        // On error, show onboarding to be safe
        setNeedsOnboarding(true);
      } finally {
        console.info('[RootLayout] Config check complete, setting ready');
        setIsReady(true);
      }
    }

    checkInitialConfig();
  }, [])

  useEffect(() => {
    // On native platforms, load theme preference from AsyncStorage
    // (On web, it's already loaded synchronously in the atom)
    if (Platform.OS !== 'web') {
      AsyncStorage.getItem('theme-preference').then(stored => {
        if (stored) {
          const preference = JSON.parse(stored);
          setThemePreference(preference);
        }
      }).catch(e => console.error('[RootLayout] Init (native) - error loading theme preference:', e));
    }

    // Initialize system theme
    const systemTheme = getPrefferedColor();
    setSystemTheme(systemTheme);

    // wire up event listeners for platform changes
    if (Platform.OS === 'web') {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      const handler = (e: MediaQueryListEvent) => {
        const newTheme = e.matches ? 'dark' : 'light';
        setSystemTheme(newTheme);
      };
      mediaQuery.addEventListener('change', handler);
      return () => mediaQuery.removeEventListener('change', handler);
    } else {
      const subscription = Appearance.addChangeListener((ch) => {
        const newTheme = ch.colorScheme === 'dark' ? 'dark' : 'light';
        setSystemTheme(newTheme);
      });
      return () => subscription.remove();
    }
  }, [setSystemTheme, setThemePreference])

  // Apply resolved theme to NativeWind
  useEffect(() => {
    setColorScheme(resolvedTheme);
  }, [resolvedTheme, setColorScheme]);

  // Show loading screen while checking configuration
  if (!isReady) {
    return (
      <View className="flex-1 bg-background items-center justify-center">
        <ActivityIndicator size="large" />
      </View>
    );
  }

  // If onboarding is needed, show onboarding screen directly
  if (needsOnboarding) {
    return (
      <GestureHandlerRootView style={{ flex: 1 }}>
        <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
          <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
          <OnboardingScreen />
        </ThemeProvider>
      </GestureHandlerRootView>
    );
  }

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <BottomSheetModalProvider>
        <QueryClientProvider client={queryClient}>
          <PlayerProvider>
            <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
              <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
              {Platform.OS === 'web' ? (
                // Web: Sidebar + Content layout with player bar at bottom
                <View className="flex-1 flex-col">
                  <View className="flex-1 flex-row">
                    <Sidebar />
                    <View className="flex-1">
                      <Stack screenOptions={{ headerShown: false }} />
                    </View>
                  </View>
                  <WebPlayerBar />
                </View>
              ) : (
                // Mobile: Full-screen Stack
                <View className="flex-1 flex-col">
                  <View className="flex-1">
                    <Stack screenOptions={{ headerShown: false }} />
                  </View>
                </View>
              )}
              <PortalHost />
              <ToastContainer />
            </ThemeProvider>
          </PlayerProvider>
        </QueryClientProvider>
      </BottomSheetModalProvider>
    </GestureHandlerRootView>
  );
}

export default function RootLayout() {
  return (
    <Provider>
      <SafeAreaProvider>
        <AppContent />
      </SafeAreaProvider>
    </Provider>
  );
}
