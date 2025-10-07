/**
 * Platform-aware player hook.
 * Metro bundler automatically selects the correct implementation:
 * - use-player.web.ts for web
 * - use-player.native.ts for iOS/Android
 *
 * Both implementations expose the same interface for consistency.
 */
export { usePlayer, usePlayerActions } from './use-player.native';
