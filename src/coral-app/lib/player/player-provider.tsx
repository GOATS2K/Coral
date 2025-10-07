/**
 * Platform-aware player provider.
 * Metro bundler automatically selects the correct implementation:
 * - player-provider.web.tsx for web (Web Audio API)
 * - player-provider.native.tsx for iOS/Android (dual expo-audio players)
 */
export { PlayerProvider, useNativePlayerContext } from './player-provider.native';
