import '@/global.css';

import { QueryClientProvider } from '@tanstack/react-query';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { Provider } from 'jotai';
import { ActivityIndicator } from 'react-native';
import { queryClient } from '@/lib/query-client';
import { useAppState } from '@/lib/hooks/use-app-state';
import { useThemeSync } from '@/lib/hooks/use-theme-sync';
import { UnauthenticatedShell } from '@/components/shells/unauthenticated-shell';
import { AuthenticatedShell } from '@/components/shells/authenticated-shell';
import { DebouncedLoader } from '@/components/debounced-loader';
import OnboardingScreen from './onboarding';
import LoginScreen from './(auth)/login';
import SetupScreen from './(auth)/setup';
import LibrarySetupScreen from './library-setup';

export {
  // Catch any errors thrown by the Layout component.
  ErrorBoundary,
} from 'expo-router';

function AppContent() {
  const { appState, setAppState } = useAppState();
  useThemeSync();

  if (appState === 'loading') {
    return (
      <DebouncedLoader isLoading={true}>
        <ActivityIndicator size="large" />
      </DebouncedLoader>
    );
  }

  if (appState === 'onboarding') {
    return (
      <UnauthenticatedShell>
        <OnboardingScreen />
      </UnauthenticatedShell>
    );
  }

  if (appState === 'setup') {
    return (
      <UnauthenticatedShell>
        <SetupScreen />
      </UnauthenticatedShell>
    );
  }

  if (appState === 'login') {
    return (
      <UnauthenticatedShell>
        <LoginScreen />
      </UnauthenticatedShell>
    );
  }

  if (appState === 'library-setup') {
    return (
      <UnauthenticatedShell>
        <LibrarySetupScreen onComplete={() => setAppState('authenticated')} />
      </UnauthenticatedShell>
    );
  }

  return <AuthenticatedShell />;
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
