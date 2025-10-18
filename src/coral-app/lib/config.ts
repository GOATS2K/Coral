import AsyncStorage from '@react-native-async-storage/async-storage';

const CONFIG_KEYS = {
  SERVER_URL: 'server_url',
  FIRST_RUN: 'first_run',
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
}
