import { Platform } from 'react-native';
import { View } from 'react-native';

/**
 * Custom title bar for Electron app with draggable region.
 * The OS will overlay native window controls on top.
 *
 * Platform-specific behavior:
 * - macOS (hiddenInset): Traffic light buttons on the left (~70px), draggable area
 * - Windows (titleBarOverlay): Native controls on the right (~138px), overlaid on our UI
 * - Linux (hidden): Varies by desktop environment, typically right (~138px)
 *
 * Only renders when running in Electron.
 */
export function TitleBar() {
  // Only show on web platform (Electron runs as web)
  if (Platform.OS !== 'web') {
    return null;
  }

  // Only show when running in Electron
  if (typeof window === 'undefined' || !(window as any).electronAPI) {
    return null;
  }

  // Detect platform for control positioning
  const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
  const isLinux = navigator.platform.toUpperCase().indexOf('LINUX') >= 0;

  // Reserve space for native controls
  // macOS has controls on the left (traffic lights), Windows/Linux on the right
  const controlsOnLeft = isMac;
  const controlWidth = isMac ? 70 : 138; // macOS traffic lights are narrower than Windows controls

  return (
    <View
      className="bg-background border-b border-border flex-row items-center"
      style={{
        height: 33, // Tall enough to show border below Windows controls (controls are ~32px)
        // @ts-expect-error - webkit-app-region is not in React Native types but works in Electron
        WebkitAppRegion: 'drag',
      }}
    >
      {/* Reserve space for native window controls */}
      {controlsOnLeft && <View style={{ width: controlWidth }} />}
      <View className="flex-1" />
      {!controlsOnLeft && <View style={{ width: controlWidth }} />}
    </View>
  );
}
