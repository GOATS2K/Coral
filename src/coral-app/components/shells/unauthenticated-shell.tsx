import { useColorScheme } from 'nativewind';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { ThemeProvider } from '@react-navigation/native';
import { StatusBar } from 'expo-status-bar';
import { NAV_THEME } from '@/lib/theme';

interface UnauthenticatedShellProps {
  children: React.ReactNode;
}

/**
 * Shell wrapper for unauthenticated screens (onboarding, setup, login, library-setup).
 * Provides theme context and gesture handling without the full app chrome.
 */
export function UnauthenticatedShell({ children }: UnauthenticatedShellProps) {
  const { colorScheme } = useColorScheme();

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <ThemeProvider value={NAV_THEME[colorScheme ?? 'dark']}>
        <StatusBar style={colorScheme === 'dark' ? 'light' : 'dark'} />
        {children}
      </ThemeProvider>
    </GestureHandlerRootView>
  );
}
