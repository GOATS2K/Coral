/**
 * Player context utilities.
 *
 * The actual context implementation is platform-specific:
 * - player-provider.web.tsx exports WebPlayerContext
 * - player-provider.native.tsx exports NativePlayerContext
 *
 * Metro bundler automatically selects the correct implementation.
 */
export { useWebPlayerContext as usePlayerContext } from './player-provider';
export type { WebPlayerContext as PlayerContext } from './player-provider.web';
export type { NativePlayerContext } from './player-provider.native';
