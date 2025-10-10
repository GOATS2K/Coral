PLAYER ARCHITECTURE REFACTOR PLAN - BATCHED APPROACH
=====================================================

## PROBLEMS IDENTIFIED

### 1. Duplicate Queue Syncs (Primary Bug)
- Multiple components (7+) calling usePlayer() each run queue sync effect
- Each has its own lastQueueRef that starts empty
- When playerStateAtom updates, all 7+ components detect "change" and call player.updateQueue()
- Results in 7+ identical calls per queue change (seen in logs: 2,841 lines of duplicate syncs)
- Stack traces show: commitHookEffectListMount → all components mounting their effects

### 2. State Duplication
Current state exists in TWO places:

Jotai Atom (playerStateAtom):
- currentTrack: SimpleTrackDto | null
- queue: SimpleTrackDto[]
- currentIndex: number
- activePlayer: 'A' | 'B'
- repeat: RepeatMode
- isShuffled: boolean
- originalQueue: SimpleTrackDto[] | null
- initializer: PlaybackInitializer | null

WebAudioPlayer instance:
- tracks: SimpleTrackDto[]          (DUPLICATE of atom.queue)
- currentTrackIndex: number          (DUPLICATE of atom.currentIndex)
- repeatMode: RepeatMode             (DUPLICATE of atom.repeat)
- isPlaying: boolean                 (not in atom)
- Plus audio engine internals (audioContext, gainNode, etc.)

This requires manual synchronization and is error-prone.

### 3. Performance Issues
- 7+ polling intervals running at 250ms (28+ calls/sec total)
- Polling effect has bad dependencies: [player, setState, position, duration, isPlaying]
  - position/duration/isPlaying change every 250ms
  - Effect re-runs 28+ times/sec, re-registering callbacks each time
  - Interval cleared and recreated 28+ times/sec
- 7+ components re-render on every position update (28+ re-renders/sec)
- High memory pressure from constant closure creation

### 4. Scattered State Management
- Queue manipulation in player-queue-utils.ts updates atom only
- Player methods update player instance only
- Manual dual updates required (play() must call setState + player.loadQueue)
- Sync logic scattered across multiple usePlayer() instances


## ARCHITECTURAL DECISIONS

### 1. Atom Structure (Simplified)
Keep ONLY queue management state:
- queue: SimpleTrackDto[]
- currentIndex: number
- originalQueue: SimpleTrackDto[] | null
- isShuffled: boolean
- repeat: RepeatMode
- activePlayer: 'A' | 'B'  (kept for native gapless switching)

Remove:
- currentTrack (derive as queue[currentIndex])
- initializer (move to separate atom if needed)

Add:
- Built-in write functions for queue operations (shuffle, unshuffle, reorder)

### 2. Player as Pure Audio Engine
WebAudioPlayer should NOT store duplicated state:
- Remove: tracks, currentTrackIndex, repeatMode (read from atom instead)
- Keep: Audio engine internals (audioContext, gainNode, buffers, etc.)
- Add: EventEmitter for state changes
- Emit events: 'playbackStateChanged', 'positionChanged', 'trackChanged', etc.

### 3. Single Coordination Point (Provider)
All synchronization happens in PlayerProvider:
- ONE polling interval (not 7+)
- ONE subscription to atom changes → updates player
- ONE subscription to player events → updates ephemeral state
- Stable effect dependencies (never re-runs)

### 4. Pure Accessor Hook
usePlayer() becomes simple:
- No useEffects
- No local state management
- Just reads atoms and returns player methods
- No sync logic

### 5. Queue Operations in Atom
Move player-queue-utils functions INTO atom write functions:
- setState becomes single dispatcher for queue changes
- No need to pass player to utility functions
- Single source of truth for queue mutations


## BATCHED IMPLEMENTATION PLAN

=============================================================================
BATCH 1: FOUNDATION (Low Risk - Additive Only)
=============================================================================

### Goal
Add EventEmitter infrastructure and ephemeral state atom WITHOUT changing existing behavior.

### What Gets Done
1. Install eventemitter3 package
2. Make WebAudioPlayer extend EventEmitter
3. Add event emissions alongside existing callbacks
4. Create playbackStateAtom for ephemeral state

### Files Modified
1. package.json
2. lib/player/web-audio-player.ts
3. lib/state.ts

### Implementation

#### 1. Install Package
```bash
bun add eventemitter3
```

#### 2. Update web-audio-player.ts

Add imports:
```typescript
import EventEmitter from 'eventemitter3';
```

Add event interface and extend EventEmitter:
```typescript
export interface PlayerEvents {
  playbackStateChanged: { isPlaying: boolean };
  trackChanged: { index: number };
}

export class WebAudioPlayer extends EventEmitter<PlayerEvents> {
  constructor() {
    super();  // Initialize EventEmitter
    this.audioContext = new AudioContext();
    this.gainNode = this.audioContext.createGain();
    this.gainNode.connect(this.audioContext.destination);
  }
```

Add event emissions (KEEP existing trackChangeCallback!):
```typescript
// In play() method
play() {
  if (this.audioContext.state === 'suspended') {
    this.audioContext.resume();
  }
  this.isPlaying = true;
  this.emit('playbackStateChanged', { isPlaying: true });  // ADD THIS
}

// In pause() method
pause() {
  this.isPlaying = false;
  this.audioContext.suspend();
  this.emit('playbackStateChanged', { isPlaying: false });  // ADD THIS
}

// In playTrackAtIndex() - after setting currentTrackIndex
private async playTrackAtIndex(index: number): Promise<void> {
  this.clearScheduledSources();
  this.currentTrackIndex = index;

  this.pruneOldCacheEntries();
  await this.scheduleTrack(index, this.audioContext.currentTime);

  // KEEP this:
  if (this.trackChangeCallback) {
    this.trackChangeCallback(index);
  }

  // ADD this:
  this.emit('trackChanged', { index });

  this.prefetchUpcomingTracks(index);
}
```

#### 3. Create playbackStateAtom in lib/state.ts

Add at end of file:
```typescript
// Ephemeral playback state (updates 4x/sec)
export interface PlaybackState {
  position: number;
  duration: number;
  isPlaying: boolean;
}

export const playbackStateAtom = atom<PlaybackState>({
  position: 0,
  duration: 0,
  isPlaying: false
});
```

### Verification Steps
```bash
# 1. App compiles
bun run web

# 2. Check browser console - should see no errors

# 3. Test playback
- Play a track - should work normally
- Pause/resume - should work
- Skip tracks - should work

# 4. Verify events are emitting (temp debug code)
Add to player-provider.web.tsx temporarily:
  useEffect(() => {
    if (!player) return;
    player.on('playbackStateChanged', (data) => console.log('EVENT:', data));
    player.on('trackChanged', (data) => console.log('TRACK:', data));
  }, [player]);

Should see events in console when playing/pausing/skipping.
Remove debug code after verification.
```

### Risk Level: LOW
- Purely additive changes
- No existing behavior changed
- Easy to rollback

### Estimated Time: 15-20 minutes

=============================================================================
BATCH 2: CENTRALIZE SYNC (Medium Risk)
=============================================================================

### Goal
Move all synchronization to provider. Old sync in usePlayer still runs (redundant but safe).

### What Gets Done
1. Add polling interval to provider
2. Add atom → player sync to provider
3. Add player → atom event listeners to provider
4. Keep old usePlayer sync (will remove in Batch 3)

### Files Modified
1. lib/player/player-provider.web.tsx

### Implementation

Replace player-provider.web.tsx content:

```typescript
import { createContext, useContext, ReactNode, useState, useEffect } from 'react';
import { useAtom, useSetAtom, useAtomValue } from 'jotai';
import { WebAudioPlayer } from './web-audio-player';
import { playerStateAtom, playbackStateAtom } from '@/lib/state';

export interface WebPlayerContext {
  player: WebAudioPlayer | null;
}

const PlayerContext = createContext<WebPlayerContext | null>(null);

export function PlayerProvider({ children }: { children: ReactNode }) {
  const [player, setPlayer] = useState<WebAudioPlayer | null>(null);
  const [state, setState] = useAtom(playerStateAtom);
  const setPlaybackState = useSetAtom(playbackStateAtom);

  // Initialize Web Audio Player
  useEffect(() => {
    console.info('[PlayerProvider] Initializing Web Audio Player...');
    let mounted = true;

    const audioPlayer = new WebAudioPlayer();

    if (mounted) {
      console.info('[PlayerProvider] Web Audio Player ready');
      setPlayer(audioPlayer);
    }

    return () => {
      console.info('[PlayerProvider] Cleanup');
      mounted = false;
      if (audioPlayer) {
        audioPlayer.destroy();
      }
    };
  }, []);

  // ONE polling interval for entire app
  useEffect(() => {
    if (!player) return;

    console.info('[PlayerProvider] Setting up polling interval');
    const interval = setInterval(() => {
      const isPlaying = player.getIsPlaying();

      if (isPlaying) {
        setPlaybackState({
          position: player.getCurrentTime(),
          duration: player.getDuration(),
          isPlaying: true
        });
      }
    }, 250);

    return () => {
      console.info('[PlayerProvider] Clearing polling interval');
      clearInterval(interval);
    };
  }, [player, setPlaybackState]); // Stable - never re-runs!

  // Atom → Player sync
  useEffect(() => {
    if (!player || state.queue.length === 0) return;

    console.info('[PlayerProvider] Syncing atom → player');
    player.updateQueue(state.queue, state.currentIndex);
  }, [player, state.queue, state.currentIndex]);

  // Sync repeat mode
  useEffect(() => {
    if (!player) return;
    player.setRepeatMode(state.repeat);
  }, [player, state.repeat]);

  // Player → Atom sync (via events)
  useEffect(() => {
    if (!player) return;

    const handleTrackChange = (data: { index: number }) => {
      console.info('[PlayerProvider] Player event: trackChanged to', data.index);
      setState((prev) => ({
        ...prev,
        currentIndex: data.index
      }));
    };

    const handlePlaybackStateChange = (data: { isPlaying: boolean }) => {
      console.info('[PlayerProvider] Player event: playbackStateChanged to', data.isPlaying);
      setPlaybackState((prev) => ({
        ...prev,
        isPlaying: data.isPlaying
      }));
    };

    player.on('trackChanged', handleTrackChange);
    player.on('playbackStateChanged', handlePlaybackStateChange);

    return () => {
      player.off('trackChanged', handleTrackChange);
      player.off('playbackStateChanged', handlePlaybackStateChange);
    };
  }, [player, setState, setPlaybackState]);

  return (
    <PlayerContext.Provider value={{ player }}>
      {children}
    </PlayerContext.Provider>
  );
}

export function useWebPlayerContext() {
  const context = useContext(PlayerContext);
  if (!context) {
    throw new Error('useWebPlayerContext must be used within PlayerProvider');
  }
  return context;
}

// Alias for native compatibility
export const useNativePlayerContext = useWebPlayerContext;
```

### Verification Steps
```bash
# 1. Check logs for provider messages
Should see:
- "[PlayerProvider] Setting up polling interval" (ONCE)
- "[PlayerProvider] Syncing atom → player" (when queue changes)
- "[PlayerProvider] Player event: trackChanged" (when skipping)

# 2. Verify only ONE polling interval
Open DevTools → Sources → Pause execution
Check call stack - should see only ONE setInterval callback

# 3. Test all playback functionality
- Play album
- Pause/resume
- Skip forward/backward
- Seek
- Progress bar updates
- Everything should work normally

# 4. Check for duplicate syncs (SHOULD STILL EXIST - that's OK!)
Play an album, check logs:
- Should see "[usePlayer] Queue changed, syncing to player" from old code (7+ times)
- Should ALSO see "[PlayerProvider] Syncing atom → player" from new code (1 time)
- This redundancy is intentional - we'll remove old sync in Batch 3
```

### Risk Level: MEDIUM
- New sync runs alongside old sync (redundant but safe)
- Can verify new sync works before removing old

### Estimated Time: 20-25 minutes

=============================================================================
BATCH 3: SIMPLIFY HOOK (Medium Risk)
=============================================================================

### Goal
Remove ALL sync/polling logic from usePlayer. Provider handles everything now.

### What Gets Done
1. Remove polling useEffect from usePlayer
2. Remove queue sync useEffect from usePlayer
3. Remove repeat sync useEffect from usePlayer
4. Remove lastQueueRef
5. Remove local state (position, duration, isPlaying)
6. Use playbackStateAtom instead

### Files Modified
1. lib/player/use-player.web.ts

### Implementation

Replace use-player.web.ts with simplified version:

```typescript
import { useCallback, useState } from 'react';
import { useAtom, useAtomValue, useSetAtom } from 'jotai';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { playerStateAtom, playbackStateAtom, PlaybackInitializer } from '@/lib/state';
import { usePlayerContext } from './player-context';
import type { WebPlayerContext } from './player-provider.web';

export function usePlayer() {
  const { player } = usePlayerContext() as WebPlayerContext;
  const [state, setState] = useAtom(playerStateAtom);
  const playbackState = useAtomValue(playbackStateAtom);
  const [isMuted, setIsMuted] = useState(false);

  // Derive current track
  const currentTrack = state.queue[state.currentIndex] || null;

  const play = useCallback(async (tracks: SimpleTrackDto[], startIndex: number = 0, initializer?: PlaybackInitializer) => {
    console.info('[usePlayer] play called', { player: !!player, trackCount: tracks.length, startIndex });

    if (!player) {
      console.warn('[usePlayer] player is null, cannot play');
      return;
    }

    const track = tracks[startIndex];

    console.info('[usePlayer] Setting player state...');
    setState({
      queue: tracks,
      currentIndex: startIndex,
      activePlayer: 'A',
      repeat: 'off',
      isShuffled: false,
      originalQueue: null,
    });

    console.info('[usePlayer] Calling loadQueue...');
    await player.loadQueue(tracks, startIndex);
    console.info('[usePlayer] loadQueue completed');
  }, [player, setState]);

  const togglePlayPause = useCallback(async () => {
    if (!player) return;
    await player.togglePlayPause();
  }, [player]);

  const skip = useCallback(async (direction: 1 | -1) => {
    if (!player) return;
    await player.skip(direction);
  }, [player]);

  const seekTo = useCallback(async (newPosition: number) => {
    if (!player) return;
    player.seekTo(newPosition);
  }, [player]);

  const playFromIndex = useCallback(async (index: number) => {
    if (!player || index < 0 || index >= state.queue.length) return;

    setState((prev) => ({ ...prev, currentIndex: index }));
    await player.playFromIndex(index);
  }, [player, state.queue.length, setState]);

  const setVolume = useCallback((volume: number) => {
    if (!player) return;
    player.setVolume(volume);
  }, [player]);

  const toggleMute = useCallback(() => {
    if (!player) return;
    const newMutedState = !isMuted;
    setIsMuted(newMutedState);
    player.setVolume(newMutedState ? 0 : 1);
  }, [player, isMuted]);

  return {
    // From playerStateAtom
    queue: state.queue,
    currentIndex: state.currentIndex,
    currentTrack,
    repeat: state.repeat,
    isShuffled: state.isShuffled,

    // From playbackStateAtom
    position: playbackState.position,
    duration: playbackState.duration,
    isPlaying: playbackState.isPlaying,

    // UI-only state
    volume: player?.getVolume() || 1,
    isMuted,

    // Actions
    play,
    togglePlayPause,
    skip,
    seekTo,
    setVolume,
    toggleMute,
    playFromIndex,
  };
}

// Web doesn't need separate actions hook
export const usePlayerActions = usePlayer;
```

### Verification Steps
```bash
# 1. Check logs - duplicate syncs should be GONE
Play an album, check console:
- Should NO LONGER see "[usePlayer] Queue changed, syncing to player" × 7+
- Should see "[PlayerProvider] Syncing atom → player" × 1 only

# 2. Count polling intervals
DevTools → Sources → Pause
- Should see ONLY 1 setInterval in call stack (from provider)
- No intervals from usePlayer components

# 3. Test all functionality thoroughly
- Play album - works ✓
- Pause/resume - works ✓
- Skip forward/backward - works ✓
- Seek - works ✓
- Progress bar updates smoothly - works ✓
- Volume control - works ✓
- Play from queue - works ✓

# 4. Check performance
Open React DevTools Profiler:
- Record during playback
- Components should re-render MUCH less
- No constant effect re-runs

# 5. Test edge cases
- Switch tabs (background playback should continue)
- Skip rapidly through tracks
- Seek multiple times quickly
```

### Risk Level: MEDIUM
- Removes old sync logic (relies on new provider sync)
- Touches all components using usePlayer
- But provider sync already verified in Batch 2

### Estimated Time: 15-20 minutes

=============================================================================
BATCH 4: CENTRALIZE ACTIONS (Higher Risk)
=============================================================================

### Goal
Move all queue manipulation logic into atom reducer. Delete player-queue-utils.ts.

### What Gets Done
1. Convert playerStateAtom to reducer pattern with actions
2. Implement all queue actions (shuffle, reorder, etc.)
3. Update all component call sites
4. Delete player-queue-utils.ts

### Files Modified
1. lib/state.ts
2. components/player/web-player-bar.tsx
3. components/player/player-controls.tsx
4. components/player/player-queue.tsx

### Files Deleted
1. lib/player/player-queue-utils.ts

### Implementation

#### 1. Update lib/state.ts

Replace playerStateAtom definition:

```typescript
// Queue action types
export type QueueAction =
  | { type: 'setQueue'; queue: SimpleTrackDto[]; index: number }
  | { type: 'shuffle' }
  | { type: 'unshuffle' }
  | { type: 'reorder'; from: number; to: number }
  | { type: 'addToQueue'; track: SimpleTrackDto }
  | { type: 'addMultipleToQueue'; tracks: SimpleTrackDto[] }
  | { type: 'removeFromQueue'; index: number }
  | { type: 'cycleRepeat' };

// Player state with reducer
export const playerStateAtom = atom(
  {
    queue: [] as SimpleTrackDto[],
    currentIndex: 0,
    originalQueue: null as SimpleTrackDto[] | null,
    isShuffled: false,
    repeat: 'off' as RepeatMode,
    activePlayer: 'A' as 'A' | 'B',
  },
  (get, set, action: QueueAction) => {
    const state = get(playerStateAtom);

    switch (action.type) {
      case 'setQueue':
        set(playerStateAtom, {
          ...state,
          queue: action.queue,
          currentIndex: action.index,
          isShuffled: false,
          originalQueue: null
        });
        break;

      case 'shuffle': {
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
          isShuffled: true,
          originalQueue: state.queue
        });
        break;
      }

      case 'unshuffle':
        if (state.originalQueue && state.queue[state.currentIndex]) {
          const currentTrack = state.queue[state.currentIndex];
          const newIndex = state.originalQueue.findIndex(t => t.id === currentTrack.id);

          set(playerStateAtom, {
            ...state,
            queue: state.originalQueue,
            currentIndex: newIndex !== -1 ? newIndex : 0,
            isShuffled: false,
            originalQueue: null
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

        set(playerStateAtom, {
          ...state,
          queue: newQueue,
          currentIndex: newIndex
        });
        break;
      }

      case 'addToQueue':
        set(playerStateAtom, {
          ...state,
          queue: [...state.queue, action.track],
          originalQueue: state.originalQueue ? [...state.originalQueue, action.track] : null
        });
        break;

      case 'addMultipleToQueue':
        set(playerStateAtom, {
          ...state,
          queue: [...state.queue, ...action.tracks],
          originalQueue: state.originalQueue ? [...state.originalQueue, ...action.tracks] : null
        });
        break;

      case 'removeFromQueue': {
        const newQueue = state.queue.filter((_, i) => i !== action.index);
        let newIndex = state.currentIndex;

        if (action.index < state.currentIndex) {
          newIndex--;
        } else if (action.index === state.currentIndex) {
          newIndex = Math.min(newIndex, newQueue.length - 1);
        }

        set(playerStateAtom, {
          ...state,
          queue: newQueue,
          currentIndex: Math.max(0, newIndex)
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
);
```

#### 2. Update components/player/web-player-bar.tsx

Remove import:
```typescript
// DELETE THIS:
import { reorderQueue, shuffleQueue, cycleRepeat } from '@/lib/player/player-queue-utils';
```

Update usage:
```typescript
<PlayerControls
  isPlaying={isPlaying}
  repeat={repeat}
  isShuffled={isShuffled}
  togglePlayPause={togglePlayPause}
  skip={skip}
  shuffle={() => setState({ type: isShuffled ? 'unshuffle' : 'shuffle' })}
  cycleRepeat={() => setState({ type: 'cycleRepeat' })}
/>

<PlayerQueue
  queue={queue}
  currentIndex={currentIndex}
  reorderQueue={(fromIndex, toIndex) => setState({ type: 'reorder', from: fromIndex, to: toIndex })}
  playFromIndex={playFromIndex}
/>
```

#### 3. Delete lib/player/player-queue-utils.ts

```bash
rm lib/player/player-queue-utils.ts
```

### Verification Steps
```bash
# 1. App compiles with no errors
bun run web

# 2. Test shuffle
- Play album
- Click shuffle button
- Queue should reorder with current track at top
- Click shuffle again (unshuffle)
- Queue should restore to original order

# 3. Test repeat modes
- Click repeat button
- Should cycle: off → all → one → off
- Verify behavior matches mode

# 4. Test reorder
- Open queue
- Drag a track to new position
- Queue should reorder correctly
- Current track index should adjust if needed

# 5. Test add/remove (if exposed in UI)
- Add track to queue
- Remove track from queue
- Verify queue updates correctly

# 6. Verify provider sync
Check logs:
- "[PlayerProvider] Syncing atom → player" should fire when queue changes
- Player should have updated queue
```

### Risk Level: HIGHER
- Changes many components
- Deletes file with queue logic
- But logic is copied, not reimplemented

### Estimated Time: 25-30 minutes

=============================================================================
BATCH 5: FINAL CLEANUP (Low Risk)
=============================================================================

### Goal
Remove state duplication from player, clean up debug logging, final verification.

### What Gets Done
1. Remove duplicate state fields from WebAudioPlayer
2. Update player methods to accept state as parameters
3. Remove debug logging (stack traces, excessive console.info)
4. Delete playback-log.txt
5. Final comprehensive testing

### Files Modified
1. lib/player/web-audio-player.ts
2. lib/player/use-player.web.ts (remove debug logs)
3. lib/player/player-provider.web.tsx (remove debug logs)

### Files Deleted
1. playback-log.txt

### Implementation

#### 1. Update web-audio-player.ts

Remove duplicate state fields:
```typescript
export class WebAudioPlayer extends EventEmitter<PlayerEvents> {
  private audioContext: AudioContext;
  private gainNode: GainNode;
  // DELETE THESE:
  // private tracks: SimpleTrackDto[] = [];
  // private currentTrackIndex = 0;
  // private repeatMode: RepeatMode = 'off';
  // private trackChangeCallback: ((index: number) => void) | null = null;

  private scheduledSources: ScheduledSource[] = [];
  private isPlaying = false;
  private audioBuffers: Map<string, AudioBuffer> = new Map();
  private schedulingInProgress: Set<number> = new Set();
  private prefetchInProgress: Set<string> = new Set();

  // ADD: Current queue reference (for lookups only)
  private currentQueue: SimpleTrackDto[] = [];
  private currentIndex = 0;
  private currentRepeat: RepeatMode = 'off';
```

Update methods to use passed-in state:
```typescript
async loadQueue(tracks: SimpleTrackDto[], startIndex: number = 0, clearCache: boolean = true) {
  this.currentQueue = tracks;
  this.currentIndex = startIndex;
  this.clearScheduledSources();

  if (clearCache) {
    this.clearAudioCache();
  }

  await this.scheduleTrack(startIndex, this.audioContext.currentTime);
  this.isPlaying = true;
  this.prefetchUpcomingTracks(startIndex);
}

updateQueue(tracks: SimpleTrackDto[], currentIndex: number) {
  console.info('[WebAudio] Updating queue, new length:', tracks.length, 'current index:', currentIndex);

  this.currentQueue = tracks;
  this.currentIndex = currentIndex;

  const trackIdsInQueue = new Set(tracks.map(t => t.id));
  this.pruneCache(trackIdsInQueue);

  if (this.isPlaying) {
    this.prefetchUpcomingTracks(currentIndex);
  }
}

setRepeatMode(mode: RepeatMode) {
  console.info('[WebAudio] Setting repeat mode:', mode);
  this.currentRepeat = mode;
}

// Update all methods that reference this.tracks to use this.currentQueue
// Update all methods that reference this.currentTrackIndex to use this.currentIndex
// Update all methods that reference this.repeatMode to use this.currentRepeat

// DELETE setTrackChangeCallback method entirely
```

Update getNextIndex to use currentRepeat:
```typescript
private getNextIndex(currentIndex: number, direction: 1 | -1 = 1): number | null {
  let nextIndex = currentIndex + direction;

  if (nextIndex < 0) {
    if (this.currentRepeat === 'all') {
      return this.currentQueue.length - 1;
    }
    return null;
  }

  if (nextIndex >= this.currentQueue.length) {
    if (this.currentRepeat === 'all') {
      return 0;
    }
    return null;
  }

  return nextIndex;
}
```

Update handleTrackEnd:
```typescript
private async handleTrackEnd(scheduledSource: ScheduledSource) {
  // ... existing code ...

  if (this.currentRepeat === 'one') {
    console.info('[WebAudio] 🔁 Repeat one - restarting track');
    await this.scheduleTrack(this.currentIndex, this.audioContext.currentTime);
    return;
  }

  // ... rest of method using this.currentIndex, this.currentQueue, this.currentRepeat
}
```

#### 2. Remove debug logging

In use-player.web.ts, remove:
```typescript
// DELETE these console.info calls (keep only important ones):
console.info('[usePlayer] play called', ...);
console.info('[usePlayer] Setting player state...');
console.info('[usePlayer] Calling loadQueue...');
console.info('[usePlayer] loadQueue completed');
```

In player-provider.web.tsx, remove excessive logs:
```typescript
// KEEP these:
console.info('[PlayerProvider] Initializing Web Audio Player...');
console.info('[PlayerProvider] Web Audio Player ready');
console.info('[PlayerProvider] Cleanup');

// DELETE these:
console.info('[PlayerProvider] Setting up polling interval');
console.info('[PlayerProvider] Clearing polling interval');
console.info('[PlayerProvider] Syncing atom → player');
console.info('[PlayerProvider] Player event: trackChanged to', ...);
console.info('[PlayerProvider] Player event: playbackStateChanged to', ...);
```

#### 3. Delete debug files
```bash
rm playback-log.txt
```

### Verification Steps - COMPREHENSIVE
```bash
# 1. Clean build
bun run web

# 2. Full playback test suite
- [ ] Play album
- [ ] Pause/resume
- [ ] Skip forward multiple times
- [ ] Skip backward
- [ ] Seek to position
- [ ] Play from middle of album
- [ ] Progress bar updates smoothly
- [ ] Volume control works
- [ ] Mute/unmute works

# 3. Queue operations
- [ ] Shuffle on
- [ ] Shuffle off
- [ ] Reorder tracks by dragging
- [ ] Play from queue item
- [ ] Repeat: off → all → one → off

# 4. Edge cases
- [ ] Skip rapidly through tracks (no crashes)
- [ ] Switch to background tab (playback continues)
- [ ] Switch back to tab (UI updates)
- [ ] Play very short track (transitions correctly)
- [ ] Play long track (no issues)

# 5. Performance checks
Open React DevTools Profiler:
- [ ] Record during playback
- [ ] Very few re-renders during playback
- [ ] No effect re-run loops
- [ ] Stable memory usage

Open DevTools Performance:
- [ ] Record for 30 seconds
- [ ] Should see only 1 polling interval
- [ ] Minimal React overhead
- [ ] No memory leaks

# 6. Console checks
- [ ] No errors
- [ ] No warnings
- [ ] Minimal logging (only important events)
- [ ] No duplicate sync messages

# 7. Browser DevTools → Sources
Pause execution during playback:
- [ ] Only 1 setInterval in call stack
- [ ] From PlayerProvider only
```

### Risk Level: LOW
- Mostly cleanup and deletions
- Core functionality already verified in previous batches

### Estimated Time: 15-20 minutes

=============================================================================


## COMPLEXITY REDUCTION SUMMARY

### Lines of Code
- Before: ~1,082 lines
- After: ~880 lines
- Reduction: -19%

### React Hooks (in usePlayer)
- Before: 13 hooks
- After: 3 hooks
- Reduction: -77%

### State Duplication
- Before: 3 copies (atom, player, component)
- After: 1 canonical (atom only)
- Reduction: -67%

### Performance
- Before: 28+ calls/sec, 28+ re-renders/sec
- After: 4 calls/sec, ~8 re-renders/sec
- Improvement: ~85% reduction in overhead


## PERFORMANCE IMPROVEMENTS

### Before
- 7+ polling intervals @ 250ms = 28+ calls/sec
- 7+ effect re-runs @ 250ms = 28+ re-runs/sec
- 7+ callback re-registrations = 28/sec
- 7+ component re-renders = 28/sec
- 7× duplicate queue syncs per change

### After
- 1 polling interval @ 250ms = 4 calls/sec (-85%)
- 0 effect re-runs (stable deps) = 0/sec (-100%)
- 0 callback re-registrations = 0/sec (-100%)
- ~2 component re-renders = ~8/sec (-70%)
- 1× queue sync per change (-85%)

### Memory
- Before: 7 timers, 28+ closures/sec, high GC pressure
- After: 1 timer, stable closures, minimal GC

Overall: ~85% reduction in React overhead during playback


## FILES SUMMARY

### Modified
1. package.json - add eventemitter3
2. lib/player/web-audio-player.ts - EventEmitter, events, remove duplication
3. lib/player/player-provider.web.tsx - centralize all sync
4. lib/state.ts - add playbackStateAtom, convert playerStateAtom to reducer
5. lib/player/use-player.web.ts - simplify to pure accessor
6. components/player/web-player-bar.tsx - use atom actions
7. components/player/player-controls.tsx - use atom actions
8. components/player/player-queue.tsx - use atom actions

### Deleted
1. lib/player/player-queue-utils.ts - logic moved to atom
2. playback-log.txt - debug file


## NOTES

### Important Technical Decisions

**setInterval vs requestAnimationFrame:**
- Keep setInterval @ 250ms for polling
- RAF causes excessive updates on high-refresh displays (144Hz = 144 calls/sec)
- RAF throttled/paused in background tabs (bad for music player)
- setInterval @ 250ms is optimal balance (4Hz updates)

**Atom Actions Pattern:**
- Allows future optimizations (undo/redo, logging, persistence)
- Testable without React
- Single source of truth for all mutations
- Type-safe with TypeScript

**Event-Driven Player:**
- Enables future Web Worker integration
- Better code splitting
- Cleaner native player integration
- Observable state changes for debugging

### Migration Notes

**Breaking Changes:** None - API remains the same from consumer perspective

**Backward Compatibility:**
- usePlayer() hook returns same interface
- Component usage unchanged
- Only internal implementation differs

### Testing Strategy

**Unit Tests:**
- Atom actions (pure functions, no React needed)
- Player events (EventEmitter patterns)

**Integration Tests:**
- Provider coordination (atom ↔ player sync)
- Event flow (player → provider → atom)

**E2E Tests:**
- Full playback scenarios
- Queue operations
- Edge cases
