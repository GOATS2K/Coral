import { useEffect } from 'react';
import { Appearance, Platform } from 'react-native';
import { useColorScheme } from 'nativewind';
import { useAtomValue, useSetAtom } from 'jotai';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { themeAtom, systemThemeAtom, themePreferenceAtom } from '@/lib/state';
import getPrefferedColor from '@/components/util/getPreferredColor';

/**
 * Hook that manages theme synchronization across the app.
 * Handles:
 * - Loading theme preference from AsyncStorage (native platforms)
 * - Initializing system theme detection
 * - Platform-specific change listeners (web MediaQuery / native Appearance)
 * - Syncing resolved theme to NativeWind
 * - Syncing theme to Electron titleBarOverlay
 */
export function useThemeSync() {
  const { setColorScheme } = useColorScheme();
  const resolvedTheme = useAtomValue(themeAtom);
  const setSystemTheme = useSetAtom(systemThemeAtom);
  const setThemePreference = useSetAtom(themePreferenceAtom);

  // Load theme preference and set up system theme listeners
  useEffect(() => {
    // On native platforms, load theme preference from AsyncStorage
    // (On web, it's already loaded synchronously in the atom)
    if (Platform.OS !== 'web') {
      AsyncStorage.getItem('theme-preference').then(stored => {
        if (stored) {
          const preference = JSON.parse(stored);
          setThemePreference(preference);
        }
      }).catch(e => console.error('[useThemeSync] Error loading theme preference:', e));
    }

    // Initialize system theme
    const systemTheme = getPrefferedColor();
    setSystemTheme(systemTheme);

    // Wire up event listeners for platform changes
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
  }, [setSystemTheme, setThemePreference]);

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

  return resolvedTheme;
}
