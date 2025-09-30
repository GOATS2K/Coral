import { atom } from 'jotai'
import AsyncStorage from '@react-native-async-storage/async-storage'
import { SimpleTrackDto, AlbumDto } from './client/schemas'

export type ThemePreference = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

// Get initial value from localStorage (web) synchronously
function getInitialThemePreference(): ThemePreference {
  if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
    try {
      const stored = localStorage.getItem('theme-preference');
      if (stored) {
        const parsed = JSON.parse(stored);
        console.log('[themePreferenceAtom] Initial value from localStorage:', parsed);
        return parsed;
      }
    } catch (e) {
      console.error('[themePreferenceAtom] Error reading initial value:', e);
    }
  }
  return 'system';
}

// Simple writable atom with manual persistence
export const themePreferenceAtom = atom<ThemePreference>(
  getInitialThemePreference(), // Load initial value synchronously
  (get, set, newValue: ThemePreference) => {
    console.log('[themePreferenceAtom] Setting to:', newValue);
    set(themePreferenceAtom, newValue);
    // Persist to storage
    AsyncStorage.setItem('theme-preference', JSON.stringify(newValue)).catch(e =>
      console.error('[themePreferenceAtom] Save error:', e)
    );
  }
)

// Current system theme (from OS) - writable atom
export const systemThemeAtom = atom<ResolvedTheme>(
  'dark' // default value
)

// Resolved theme (combines preference + system theme) - read-only computed atom
export const themeAtom = atom<ResolvedTheme>((get) => {
  const preference = get(themePreferenceAtom)
  const system = get(systemThemeAtom)
  console.log('[themeAtom] Computing - preference:', preference, 'system:', system);

  if (preference === 'system') {
    console.log('[themeAtom] Returning system theme:', system);
    return system
  }
  console.log('[themeAtom] Returning preference:', preference);
  return preference
})

export interface PlayerState {
  currentTrack: SimpleTrackDto | null;
  currentAlbum: AlbumDto | null;
  queue: SimpleTrackDto[];
  currentIndex: number;
}

export const playerStateAtom = atom<PlayerState>({
  currentTrack: null,
  currentAlbum: null,
  queue: [],
  currentIndex: 0,
})