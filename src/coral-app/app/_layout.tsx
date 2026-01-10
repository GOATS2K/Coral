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
import { themeAtom, systemThemeAtom, themePreferenceAtom, currentUserAtom } from '@/lib/state';
import { WebPlayerBar } from '@/components/player/web-player-bar';
import { ServerStatusBar } from '@/components/server-status-bar/server-status-bar';
import { PlayerProvider } from '@/lib/player/player-provider';
import { SignalRProvider } from '@/lib/signalr/signalr-provider';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { ToastContainer } from '@/components/toast-container';
import { Sidebar } from '@/components/ui/sidebar';
import { Config } from '@/lib/config';
import OnboardingScreen from './onboarding';
import LoginScreen from './(auth)/login';
import SetupScreen from './(auth)/setup';
import { TitleBar } from '@/components/title-bar';
import { DebouncedLoader } from '@/components/debounced-loader';
import { fetchGetStatus, fetchGetCurrentUser } from '@/lib/client/components';
import { onUnauthorized } from '@/lib/client/fetcher';
import { useAuth } from '@/lib/hooks/use-auth';

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

type AppState = 'loading' | 'onboarding' | 'setup' | 'login' | 'authenticated';

function AppContent() {
  const { colorScheme, setColorScheme } = useColorScheme();
  const resolvedTheme = useAtomValue(themeAtom);
  const setSystemTheme = useSetAtom(systemThemeAtom);
  const setThemePreference = useSetAtom(themePreferenceAtom);
  const currentUser = useAtomValue(currentUserAtom);
  const setCurrentUser = useSetAtom(currentUserAtom);
  const [appState, setAppState] = useState<AppState>('loading');
  const { initializeAuth } = useAuth();

  // Watch for auth state changes (login/logout)
  useEffect(() => {
    if (currentUser && appState !== 'authenticated') {
      console.info('[RootLayout] User logged in, transitioning to authenticated');
      setAppState('authenticated');
    }
  }, [currentUser, appState]);

  // Check configuration and auth status on app startup
  useEffect(() => {
    async function checkInitialState() {
      try {
        console.info('[RootLayout] Checking initial config...');
        const isFirstRun = await Config.isFirstRun();
        const backendUrl = await Config.getBackendUrl();

        console.info('[RootLayout] First run:', isFirstRun, 'Backend URL:', backendUrl);

        if (isFirstRun || !backendUrl) {
          console.info('[RootLayout] Onboarding needed');
          setAppState('onboarding');
          return;
        }

        // Initialize auth (load stored token/deviceId)
        await initializeAuth();

        // Check auth status from server
        console.info('[RootLayout] Checking auth status...');
        const status = await fetchGetStatus({});

        if (status.requiresSetup) {
          console.info('[RootLayout] Server requires setup');
          setAppState('setup');
        } else if (!status.isAuthenticated) {
          console.info('[RootLayout] Not authenticated');
          setAppState('login');
        } else {
          console.info('[RootLayout] Authenticated');
          const user = await fetchGetCurrentUser({});
          setCurrentUser(user);
          setAppState('authenticated');
        }
      } catch (error: any) {
        console.error('[RootLayout] Error checking initial state:', error);
        // Check if this is a network error (no server connection)
        // vs an auth error (401) which is handled by onUnauthorized listener
        if (error?.status === 'unknown' || error?.message?.includes('Network Error')) {
          // Network error - show onboarding to allow reconfiguring server URL
          setAppState('onboarding');
        } else {
          // Auth or other server error - go to login
          setAppState('login');
        }
      }
    }

    checkInitialState();
  }, [initializeAuth]);

  // Listen for 401 responses to redirect to login
  useEffect(() => {
    const unsubscribe = onUnauthorized(() => {
      console.info('[RootLayout] Received 401, redirecting to login');
      setCurrentUser(null);
      setAppState('login');
    });
    return unsubscribe;
  }, [setCurrentUser]);

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

  // Update Electron window controls theme (Windows titleBarOverlay)
  useEffect(() => {
    if (Platform.OS === 'web' && typeof window !== 'undefined') {
      const electronAPI = (window as any).electronAPI;
      if (electronAPI?.setTheme) {
        electronAPI.setTheme(resolvedTheme);
      }
    }
  }, [resolvedTheme]);

  // Show loading screen while checking configuration
  if (appState === 'loading') {
    return (
      <DebouncedLoader isLoading={true}>
        <ActivityIndicator size="large" />
      </DebouncedLoader>
    );
  }

  // Show onboarding screen for server URL configuration
  if (appState === 'onboarding') {
    return (
      <GestureHandlerRootView style={{ flex: 1 }}>
        <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
          <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
          <OnboardingScreen />
        </ThemeProvider>
      </GestureHandlerRootView>
    );
  }

  // Show setup screen for first-time account creation
  if (appState === 'setup') {
    return (
      <GestureHandlerRootView style={{ flex: 1 }}>
        <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
          <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
          <SetupScreen />
        </ThemeProvider>
      </GestureHandlerRootView>
    );
  }

  // Show login screen for unauthenticated users
  if (appState === 'login') {
    return (
      <GestureHandlerRootView style={{ flex: 1 }}>
        <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
          <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
          <LoginScreen />
        </ThemeProvider>
      </GestureHandlerRootView>
    );
  }

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <BottomSheetModalProvider>
        <PlayerProvider>
          <SignalRProvider>
            <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
              <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
              {Platform.OS === 'web' ? (
                // Web: Sidebar + Content layout with player bar at bottom
                <View className="flex-1 flex-col bg-background">
                  <TitleBar />
                  <View className="flex-1 flex-row">
                    <Sidebar />
                    <View className="flex-1">
                      <Stack screenOptions={{ headerShown: false }} />
                    </View>
                  </View>
                  <WebPlayerBar />
                  <ServerStatusBar />
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
          </SignalRProvider>
        </PlayerProvider>
      </BottomSheetModalProvider>
    </GestureHandlerRootView>
  );
}

export default function RootLayout() {
  return (
    <Provider>
      <SafeAreaProvider>
        <QueryClientProvider client={queryClient}>
          <AppContent />
        </QueryClientProvider>
      </SafeAreaProvider>
    </Provider>
  );
}
