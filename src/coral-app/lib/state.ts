import { atom } from 'jotai'
import AsyncStorage from '@react-native-async-storage/async-storage'
import { SimpleTrackDto } from './client/schemas'
import { Appearance, Platform } from 'react-native'

export type ThemePreference = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

// Get initial value from localStorage (web) synchronously
function getInitialThemePreference(): ThemePreference {
  if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
    try {
      const stored = localStorage.getItem('theme-preference');
      if (stored) {
        const parsed = JSON.parse(stored);
        return parsed;
      }
    } catch (e) {
      console.error('[themePreferenceAtom] Error reading initial value:', e);
    }
  }
  return 'system';
}

// Get initial system theme
function getInitialSystemTheme(): ResolvedTheme {
  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined' && window.matchMedia) {
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
  } else {
    return Appearance.getColorScheme() === 'dark' ? 'dark' : 'light';
  }
  return 'dark';
}

// Simple writable atom with manual persistence
export const themePreferenceAtom = atom<ThemePreference>(
  getInitialThemePreference(), // Load initial value synchronously
  (get, set, newValue: ThemePreference) => {
    set(themePreferenceAtom, newValue);
    // Persist to storage
    AsyncStorage.setItem('theme-preference', JSON.stringify(newValue)).catch(e =>
      console.error('[themePreferenceAtom] Save error:', e)
    );
  }
)

// Current system theme (from OS) - writable atom
export const systemThemeAtom = atom<ResolvedTheme>(
  getInitialSystemTheme() // Read actual system theme at initialization
)

// Resolved theme (combines preference + system theme) - read-only computed atom
export const themeAtom = atom<ResolvedTheme>((get) => {
  const preference = get(themePreferenceAtom)
  const system = get(systemThemeAtom)

  if (preference === 'system') {
    return system
  }
  return preference
})

export type RepeatMode = 'off' | 'all' | 'one';

export enum PlaybackSource {
  Album = 'album',
  Search = 'search',
  Favorites = 'favorites',
  Home = 'home',
}

export interface PlaybackInitializer {
  source: PlaybackSource;
  id: string; // Album ID, search query, etc.
}

export interface PlayerState {
  currentTrack: SimpleTrackDto | null;
  queue: SimpleTrackDto[];
  currentIndex: number;
  activePlayer: 'A' | 'B';
  repeat: RepeatMode;
  isShuffled: boolean;
  originalQueue: SimpleTrackDto[] | null;
  initializer: PlaybackInitializer | null;
}

// Queue action types
export type QueueAction =
  | { type: 'setQueue'; queue: SimpleTrackDto[]; index: number; initializer?: PlaybackInitializer | null }
  | { type: 'setCurrentIndex'; index: number }
  | { type: 'shuffle' }
  | { type: 'unshuffle' }
  | { type: 'reorder'; from: number; to: number }
  | { type: 'addToQueue'; track: SimpleTrackDto }
  | { type: 'addMultipleToQueue'; tracks: SimpleTrackDto[] }
  | { type: 'removeFromQueue'; index: number }
  | { type: 'cycleRepeat' };

// Player state with reducer pattern
export const playerStateAtom = atom(
  {
    currentTrack: null as SimpleTrackDto | null,
    queue: [] as SimpleTrackDto[],
    currentIndex: 0,
    activePlayer: 'A' as 'A' | 'B',
    repeat: 'off' as RepeatMode,
    isShuffled: false,
    originalQueue: null as SimpleTrackDto[] | null,
    initializer: null as PlaybackInitializer | null,
  },
  (get, set, action: QueueAction) => {
    const state = get(playerStateAtom);

    switch (action.type) {
      case 'setQueue':
        set(playerStateAtom, {
          ...state,
          queue: action.queue,
          currentIndex: action.index,
          currentTrack: action.queue[action.index] || null,
          isShuffled: false,
          originalQueue: null,
          initializer: action.initializer ?? null,
        });
        break;

      case 'setCurrentIndex':
        set(playerStateAtom, {
          ...state,
          currentIndex: action.index,
          currentTrack: state.queue[action.index] || null,
        });
        break;

      case 'shuffle': {
        const newShuffleState = !state.isShuffled;

        if (!newShuffleState) {
          // Turning shuffle off - restore original queue
          if (!state.originalQueue || !state.currentTrack) {
            set(playerStateAtom, { ...state, isShuffled: false, originalQueue: null });
            return;
          }

          // Find current track in original queue
          const newIndex = state.originalQueue.findIndex(t => t.id === state.currentTrack!.id);
          if (newIndex === -1) {
            set(playerStateAtom, { ...state, isShuffled: false, originalQueue: null });
            return;
          }

          const freshCurrentTrack = state.originalQueue[newIndex];
          set(playerStateAtom, {
            ...state,
            queue: state.originalQueue,
            currentIndex: newIndex,
            currentTrack: freshCurrentTrack,
            isShuffled: false,
            originalQueue: null,
          });
        } else {
          // Turning shuffle on
          const currentTrack = state.queue[state.currentIndex];
          const otherTracks = [
            ...state.queue.slice(0, state.currentIndex),
            ...state.queue.slice(state.currentIndex + 1)
          ];

          // Fisher-Yates shuffle
          const shuffled = [...otherTracks];
          for (let i = shuffled.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
          }

          const newQueue = [currentTrack, ...shuffled];
          set(playerStateAtom, {
            ...state,
            queue: newQueue,
            currentIndex: 0,
            currentTrack: currentTrack,
            isShuffled: true,
            originalQueue: state.queue,
          });
        }
        break;
      }

      case 'unshuffle':
        if (state.originalQueue && state.currentTrack) {
          const newIndex = state.originalQueue.findIndex(t => t.id === state.currentTrack!.id);
          const freshCurrentTrack = state.originalQueue[newIndex];

          set(playerStateAtom, {
            ...state,
            queue: state.originalQueue,
            currentIndex: newIndex !== -1 ? newIndex : 0,
            currentTrack: freshCurrentTrack || state.currentTrack,
            isShuffled: false,
            originalQueue: null,
          });
        }
        break;

      case 'reorder': {
        const newQueue = [...state.queue];
        const [removed] = newQueue.splice(action.from, 1);
        newQueue.splice(action.to, 0, removed);

        let newIndex = state.currentIndex;
        if (action.from === state.currentIndex) {
          newIndex = action.to;
        } else if (action.from < state.currentIndex && action.to >= state.currentIndex) {
          newIndex--;
        } else if (action.from > state.currentIndex && action.to <= state.currentIndex) {
          newIndex++;
        }

        const freshCurrentTrack = newQueue[newIndex];
        set(playerStateAtom, {
          ...state,
          queue: newQueue,
          currentIndex: newIndex,
          currentTrack: freshCurrentTrack || state.currentTrack,
        });
        break;
      }

      case 'addToQueue': {
        const newQueue = [...state.queue, action.track];
        const newOriginalQueue = state.originalQueue ? [...state.originalQueue, action.track] : null;
        const freshCurrentTrack = newQueue[state.currentIndex];

        set(playerStateAtom, {
          ...state,
          queue: newQueue,
          originalQueue: newOriginalQueue,
          currentTrack: freshCurrentTrack || state.currentTrack,
        });
        break;
      }

      case 'addMultipleToQueue': {
        const newQueue = [...state.queue, ...action.tracks];
        const newOriginalQueue = state.originalQueue ? [...state.originalQueue, ...action.tracks] : null;
        const freshCurrentTrack = newQueue[state.currentIndex];

        set(playerStateAtom, {
          ...state,
          queue: newQueue,
          originalQueue: newOriginalQueue,
          currentTrack: freshCurrentTrack || state.currentTrack,
        });
        break;
      }

      case 'removeFromQueue': {
        const newQueue = [...state.queue];
        const removedTrack = newQueue[action.index];
        newQueue.splice(action.index, 1);

        let newOriginalQueue = state.originalQueue;
        if (newOriginalQueue && removedTrack) {
          newOriginalQueue = newOriginalQueue.filter(t => t.id !== removedTrack.id);
        }

        let newCurrentIndex = state.currentIndex;
        if (action.index < state.currentIndex) {
          newCurrentIndex--;
        } else if (action.index === state.currentIndex) {
          if (action.index >= newQueue.length && newQueue.length > 0) {
            newCurrentIndex = newQueue.length - 1;
          }
        }

        newCurrentIndex = Math.max(0, newCurrentIndex);
        set(playerStateAtom, {
          ...state,
          queue: newQueue,
          currentIndex: newCurrentIndex,
          currentTrack: newQueue[newCurrentIndex] || state.currentTrack,
          originalQueue: newOriginalQueue,
        });
        break;
      }

      case 'cycleRepeat': {
        const modes: RepeatMode[] = ['off', 'all', 'one'];
        const currentIndex = modes.indexOf(state.repeat);
        const nextMode = modes[(currentIndex + 1) % modes.length];
        set(playerStateAtom, { ...state, repeat: nextMode });
        break;
      }
    }
  }
)

// Albums screen scroll state
export interface AlbumsScrollState {
  scrollPosition: number;
  savedPageCount: number;
  needsRestoration: boolean;
  firstVisibleIndex: number;
  savedFirstVisibleIndex: number;
}

export const albumsScrollStateAtom = atom<AlbumsScrollState>({
  scrollPosition: 0,
  savedPageCount: 1,
  needsRestoration: false,
  firstVisibleIndex: 0,
  savedFirstVisibleIndex: 0,
})

// Search state - stores last search query for restoration
export const lastSearchQueryAtom = atom<string>('')

// Ephemeral playback state (updates 4x/sec)
export interface PlaybackState {
  position: number;
  duration: number;
  isPlaying: boolean;
  isBuffering: boolean;
}

export const playbackStateAtom = atom<PlaybackState>({
  position: 0,
  duration: 0,
  isPlaying: false,
  isBuffering: false
})