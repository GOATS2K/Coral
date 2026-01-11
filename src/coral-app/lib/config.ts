import AsyncStorage from '@react-native-async-storage/async-storage';

const CONFIG_KEYS = {
  SERVER_URL: 'server_url',
  FIRST_RUN: 'first_run',
  ACCESS_TOKEN: 'access_token',
  DEVICE_ID: 'device_id',
} as const;

// Default server URL (development)
const DEFAULT_SERVER_URL = 'http://localhost:5031';

export class Config {
  /**
   * Get the server URL
   * @returns Promise<string> The configured server URL or default
   */
  static async getBackendUrl(): Promise<string> {
    try {
      const url = await AsyncStorage.getItem(CONFIG_KEYS.SERVER_URL);
      return url || DEFAULT_SERVER_URL;
    } catch (error) {
      console.error('Failed to get server URL:', error);
      return DEFAULT_SERVER_URL;
    }
  }

  /**
   * Set the server URL
   * @param url - The server URL to set
   */
  static async setBackendUrl(url: string): Promise<void> {
    try {
      // Normalize URL by removing trailing slash
      const normalizedUrl = url.endsWith('/') ? url.slice(0, -1) : url;
      await AsyncStorage.setItem(CONFIG_KEYS.SERVER_URL, normalizedUrl);
    } catch (error) {
      console.error('Failed to set server URL:', error);
      throw error;
    }
  }

  /**
   * Check if this is the first run
   * @returns Promise<boolean>
   */
  static async isFirstRun(): Promise<boolean> {
    try {
      const firstRun = await AsyncStorage.getItem(CONFIG_KEYS.FIRST_RUN);
      return firstRun === null; // If key doesn't exist, it's first run
    } catch (error) {
      console.error('Failed to check first run:', error);
      return true;
    }
  }

  /**
   * Mark first run as complete
   */
  static async completeFirstRun(): Promise<void> {
    try {
      await AsyncStorage.setItem(CONFIG_KEYS.FIRST_RUN, 'false');
    } catch (error) {
      console.error('Failed to mark first run complete:', error);
      throw error;
    }
  }

  /**
   * Check if running in Electron
   * @returns boolean
   */
  static isElectron(): boolean {
    // Check if we're running in Electron by looking for the electron API
    return typeof window !== 'undefined' && 'electronAPI' in window;
  }

  /**
   * Reset all configuration (useful for testing/debugging)
   */
  static async reset(): Promise<void> {
    try {
      await AsyncStorage.multiRemove(Object.values(CONFIG_KEYS));
    } catch (error) {
      console.error('Failed to reset config:', error);
      throw error;
    }
  }

  /**
   * Get all configuration (for debugging)
   */
  static async getAll(): Promise<Record<string, string | null>> {
    try {
      const keys = Object.values(CONFIG_KEYS);
      const values = await AsyncStorage.multiGet(keys);
      return Object.fromEntries(values);
    } catch (error) {
      console.error('Failed to get all config:', error);
      return {};
    }
  }

  // Auth-related methods

  /**
   * Get the access token (for native platforms)
   */
  static async getAccessToken(): Promise<string | null> {
    try {
      return await AsyncStorage.getItem(CONFIG_KEYS.ACCESS_TOKEN);
    } catch (error) {
      console.error('Failed to get access token:', error);
      return null;
    }
  }

  /**
   * Set the access token (for native platforms)
   */
  static async setAccessToken(token: string): Promise<void> {
    try {
      await AsyncStorage.setItem(CONFIG_KEYS.ACCESS_TOKEN, token);
    } catch (error) {
      console.error('Failed to set access token:', error);
      throw error;
    }
  }

  /**
   * Get the device ID (server-assigned)
   */
  static async getDeviceId(): Promise<string | null> {
    try {
      return await AsyncStorage.getItem(CONFIG_KEYS.DEVICE_ID);
    } catch (error) {
      console.error('Failed to get device ID:', error);
      return null;
    }
  }

  /**
   * Set the device ID (server-assigned)
   */
  static async setDeviceId(deviceId: string): Promise<void> {
    try {
      await AsyncStorage.setItem(CONFIG_KEYS.DEVICE_ID, deviceId);
    } catch (error) {
      console.error('Failed to set device ID:', error);
      throw error;
    }
  }

  /**
   * Clear auth session data (for logout)
   * Note: Device ID is preserved so the same device is reused on next login
   */
  static async clearAuth(): Promise<void> {
    try {
      await AsyncStorage.removeItem(CONFIG_KEYS.ACCESS_TOKEN);
    } catch (error) {
      console.error('Failed to clear auth:', error);
      throw error;
    }
  }

  /**
   * Check if the user is authenticated (has a token stored)
   */
  static async isAuthenticated(): Promise<boolean> {
    try {
      const token = await AsyncStorage.getItem(CONFIG_KEYS.ACCESS_TOKEN);
      return token !== null;
    } catch (error) {
      console.error('Failed to check authentication:', error);
      return false;
    }
  }
}
