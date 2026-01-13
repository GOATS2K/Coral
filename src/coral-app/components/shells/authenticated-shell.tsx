import { Platform, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { BottomSheetModalProvider } from '@gorhom/bottom-sheet';
import { ThemeProvider } from '@react-navigation/native';
import { PortalHost } from '@rn-primitives/portal';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { NAV_THEME } from '@/lib/theme';
import { PlayerProvider } from '@/lib/player/player-provider';
import { SignalRProvider } from '@/lib/signalr/signalr-provider';
import { WebPlayerBar } from '@/components/player/web-player-bar';
import { ServerStatusBar } from '@/components/server-status-bar/server-status-bar';
import { ToastContainer } from '@/components/toast-container';
import { Sidebar } from '@/components/ui/sidebar';
import { TitleBar } from '@/components/title-bar';

/**
 * Shell wrapper for authenticated app state.
 * Includes all providers (player, signalr, bottom sheet) and platform-specific layout.
 */
export function AuthenticatedShell() {
  const { colorScheme } = useColorScheme();

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
