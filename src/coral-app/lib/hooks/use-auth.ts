import { Platform } from 'react-native';
import { useAtom, useSetAtom } from 'jotai';
import { useQueryClient } from '@tanstack/react-query';
import * as Device from 'expo-device';
import { useLogin, useRegister, useLogout } from '@/lib/client/components';
import { currentUserAtom, accessTokenAtom, deviceIdAtom } from '@/lib/state';
import { Config } from '@/lib/config';
import { setAccessToken } from '@/lib/client/fetcher';
import type { DeviceInfo, OperatingSystem } from '@/lib/client/schemas';

/**
 * Detect the browser name from userAgent
 */
function detectBrowser(): string {
  if (typeof navigator === 'undefined') return 'Browser';

  const ua = navigator.userAgent;

  // Order matters - check more specific browsers first
  if (ua.includes('Edg/')) return 'Edge';
  if (ua.includes('OPR/') || ua.includes('Opera')) return 'Opera';
  if (ua.includes('Vivaldi')) return 'Vivaldi';
  if (ua.includes('Brave')) return 'Brave';
  if (ua.includes('Chrome')) return 'Chrome';
  if (ua.includes('Firefox')) return 'Firefox';
  if (ua.includes('Safari')) return 'Safari';

  return 'Browser';
}

/**
 * Detect OS from userAgent (for web platforms)
 */
function detectOsFromUserAgent(): OperatingSystem {
  if (typeof navigator === 'undefined') return 'Windows';

  const ua = navigator.userAgent.toLowerCase();

  if (ua.includes('mac')) return 'MacOS';
  if (ua.includes('linux')) return 'Linux';
  if (ua.includes('android')) return 'Android';
  if (ua.includes('iphone') || ua.includes('ipad')) return 'iOS';

  return 'Windows';
}

/**
 * Get the system/computer name (Electron only)
 */
function getElectronSystemName(): string | null {
  if (typeof window === 'undefined' || !('electronAPI' in window)) return null;

  const electronAPI = (window as any).electronAPI;
  return electronAPI?.getSystemName?.() ?? null;
}

/**
 * Get device information for auth requests
 */
function getDeviceInfo(): DeviceInfo {
  // Native iOS
  if (Platform.OS === 'ios') {
    const name = Device.deviceName || 'iOS Device';
    return { name, type: 'Native', os: 'iOS' };
  }

  // Native Android
  if (Platform.OS === 'android') {
    const name = Device.deviceName || 'Android Device';
    return { name, type: 'Native', os: 'Android' };
  }

  // Electron desktop app
  if (Config.isElectron()) {
    const os = detectOsFromUserAgent();
    const name = getElectronSystemName() || 'Coral Desktop';
    return { name, type: 'Electron', os };
  }

  // Web browser
  const os = detectOsFromUserAgent();
  const browser = detectBrowser();
  return { name: `${browser} on ${os}`, type: 'Web', os };
}

interface UseAuthResult {
  currentUser: ReturnType<typeof useAtom<typeof currentUserAtom>>[0];
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<boolean>;
  register: (username: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  initializeAuth: () => Promise<void>;
}

export function useAuth(): UseAuthResult {
  const queryClient = useQueryClient();

  const [currentUser, setCurrentUser] = useAtom(currentUserAtom);
  const setAccessTokenAtom = useSetAtom(accessTokenAtom);
  const setDeviceId = useSetAtom(deviceIdAtom);

  const loginMutation = useLogin();
  const registerMutation = useRegister();
  const logoutMutation = useLogout();

  const isLoading = loginMutation.isPending || registerMutation.isPending || logoutMutation.isPending;

  const initializeAuth = async () => {
    try {
      // Load stored token (for native platforms)
      if (Platform.OS !== 'web') {
        const storedToken = await Config.getAccessToken();
        if (storedToken) {
          setAccessToken(storedToken);
          setAccessTokenAtom(storedToken);
        }
      }

      // Load stored device ID
      const storedDeviceId = await Config.getDeviceId();
      if (storedDeviceId) {
        setDeviceId(storedDeviceId);
      }
    } catch (error) {
      console.error('[useAuth] Failed to initialize auth:', error);
    }
  };

  const handleAuthSuccess = async (
    accessToken: string | null | undefined,
    deviceId: string,
    user: ReturnType<typeof useAtom<typeof currentUserAtom>>[0]
  ) => {
    // Store device ID
    await Config.setDeviceId(deviceId);
    setDeviceId(deviceId);

    // For non-web platforms, store and set the access token
    if (accessToken && Platform.OS !== 'web') {
      await Config.setAccessToken(accessToken);
      setAccessToken(accessToken);
      setAccessTokenAtom(accessToken);
    }

    // Set current user
    setCurrentUser(user);

    // Invalidate all queries to refetch with new auth
    await queryClient.invalidateQueries();
  };

  const login = async (username: string, password: string): Promise<boolean> => {
    try {
      const deviceInfo = getDeviceInfo();
      const existingDeviceId = await Config.getDeviceId();

      const response = await loginMutation.mutateAsync({
        body: {
          username,
          password,
          device: deviceInfo,
          deviceId: existingDeviceId,
        },
      });

      await handleAuthSuccess(response.accessToken, response.deviceId, response.user);
      return true;
    } catch (error: any) {
      console.error('[useAuth] Login failed:', error);
      return false;
    }
  };

  const register = async (username: string, password: string): Promise<boolean> => {
    try {
      const deviceInfo = getDeviceInfo();

      const response = await registerMutation.mutateAsync({
        body: {
          username,
          password,
          device: deviceInfo,
        },
      });

      await handleAuthSuccess(response.accessToken, response.deviceId, response.user);
      return true;
    } catch (error: any) {
      console.error('[useAuth] Registration failed:', error);
      return false;
    }
  };

  const logout = async () => {
    try {
      await logoutMutation.mutateAsync({});
    } catch (error) {
      // Even if the server logout fails, we should still clear local state
      console.error('[useAuth] Server logout failed:', error);
    }

    // Clear local auth state
    await Config.clearAuth();
    setAccessToken(null);
    setAccessTokenAtom(null);
    setCurrentUser(null);

    // Clear all cached queries
    queryClient.clear();
  };

  return {
    currentUser,
    isAuthenticated: currentUser !== null,
    isLoading,
    login,
    register,
    logout,
    initializeAuth,
  };
}
