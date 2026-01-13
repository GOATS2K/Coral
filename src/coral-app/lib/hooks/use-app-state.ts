import { useEffect, useState } from 'react';
import { useAtomValue, useSetAtom } from 'jotai';
import { currentUserAtom } from '@/lib/state';
import { Config } from '@/lib/config';
import { fetchGetStatus, fetchGetCurrentUser, fetchMusicLibraries } from '@/lib/client/components';
import { onUnauthorized } from '@/lib/client/fetcher';
import { useAuth } from '@/lib/hooks/use-auth';

export type AppState = 'loading' | 'onboarding' | 'setup' | 'login' | 'library-setup' | 'authenticated';

interface UseAppStateResult {
  appState: AppState;
  setAppState: (state: AppState) => void;
}

/**
 * Hook that manages the application startup state machine.
 * Handles:
 * - Initial state determination (config check, auth check, library check)
 * - Auth state transitions (login detection)
 * - 401 response handling (redirect to login)
 */
export function useAppState(): UseAppStateResult {
  const currentUser = useAtomValue(currentUserAtom);
  const setCurrentUser = useSetAtom(currentUserAtom);
  const [appState, setAppState] = useState<AppState>('loading');
  const { initializeAuth } = useAuth();

  // Watch for auth state changes (login/logout)
  // This handles transitions from login screen - NOT the initial startup check
  useEffect(() => {
    if (!currentUser) return;
    if (appState === 'authenticated' || appState === 'library-setup') return;

    // User just logged in - check if they have libraries before transitioning
    async function checkLibrariesAfterLogin() {
      console.info('[useAppState] User logged in, checking for libraries...');

      const libraries = await fetchMusicLibraries({}).catch((error) => {
        console.error('[useAppState] Error checking libraries after login:', error);
        return null;
      });

      if (libraries === null) {
        // Error occurred - default to authenticated, they can configure later
        setAppState('authenticated');
        return;
      }

      if (libraries.length === 0) {
        console.info('[useAppState] No libraries, showing library setup');
        setAppState('library-setup');
        return;
      }

      console.info('[useAppState] Has libraries, transitioning to authenticated');
      setAppState('authenticated');
    }

    checkLibrariesAfterLogin();
  }, [currentUser, appState]);

  // Check configuration and auth status on app startup
  useEffect(() => {
    async function checkInitialState() {
      try {
        console.info('[useAppState] Checking initial config...');
        const isFirstRun = await Config.isFirstRun();
        const backendUrl = await Config.getBackendUrl();

        console.info('[useAppState] First run:', isFirstRun, 'Backend URL:', backendUrl);

        if (isFirstRun || !backendUrl) {
          console.info('[useAppState] Onboarding needed');
          setAppState('onboarding');
          return;
        }

        // Initialize auth (load stored token/deviceId)
        await initializeAuth();

        // Check auth status from server
        console.info('[useAppState] Checking auth status...');
        const status = await fetchGetStatus({});

        if (status.requiresSetup) {
          console.info('[useAppState] Server requires setup');
          setAppState('setup');
          return;
        }

        if (!status.isAuthenticated) {
          console.info('[useAppState] Not authenticated');
          setAppState('login');
          return;
        }

        console.info('[useAppState] Authenticated');
        // Fetch user and libraries in parallel, then set state together to avoid race condition
        const [user, libraries] = await Promise.all([
          fetchGetCurrentUser({}),
          fetchMusicLibraries({})
        ]);

        // Set both states together so they batch - prevents useEffect race condition
        setCurrentUser(user);
        if (libraries.length === 0) {
          console.info('[useAppState] No libraries, showing library setup');
          setAppState('library-setup');
        } else {
          setAppState('authenticated');
        }
      } catch (error: any) {
        console.error('[useAppState] Error checking initial state:', error);
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
  }, [initializeAuth, setCurrentUser]);

  // Listen for 401 responses to redirect to login
  useEffect(() => {
    const unsubscribe = onUnauthorized(() => {
      console.info('[useAppState] Received 401, redirecting to login');
      setCurrentUser(null);
      setAppState('login');
    });
    return unsubscribe;
  }, [setCurrentUser]);

  return { appState, setAppState };
}
