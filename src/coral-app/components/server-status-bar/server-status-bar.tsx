import React from 'react';
import { View } from 'react-native';
import { useAtomValue } from 'jotai';
import { HubConnectionState } from '@microsoft/signalr';
import { Text } from '@/components/ui/text';
import { cn } from '@/lib/utils';
import {
  signalrConnectionStateAtom,
  activeScanProgressAtom,
} from '@/lib/signalr/signalr-atoms';
import { ScanProgressIndicator } from './scan-progress-indicator';

export function ServerStatusBar() {
  const connectionState = useAtomValue(signalrConnectionStateAtom);
  const activeScans = useAtomValue(activeScanProgressAtom);

  // Don't show if not connected and no active content
  const hasContent = activeScans.length > 0 || connectionState === HubConnectionState.Reconnecting;

  if (!hasContent) {
    return null;
  }

  return (
    <View
      className={cn(
        'mx-4 mb-4', // Margins for spacing
        'py-2 px-4', // Internal padding
        'rounded-md', // Slightly rounded corners
        'bg-background', // Normal background color
        'border', // Thin border for separation
        // Inverted border: dark mode gets white border, light mode gets black border
        'dark:border-white border-black',
        'transition-all duration-300 ease-in-out'
      )}
    >
      {/* Connection status indicator */}
      {connectionState === HubConnectionState.Reconnecting && (
        <View className="flex-row items-center justify-start">
          <View className="w-2 h-2 rounded-full bg-yellow-500 mr-2 animate-pulse" />
          <Text className="text-sm text-foreground">
            Reconnecting to server...
          </Text>
        </View>
      )}

      {/* Scan progress indicators */}
      {activeScans.map((scan) => (
        <ScanProgressIndicator
          key={scan.requestId}
          progress={scan}
        />
      ))}

      {/* Future: Add more status indicators here */}
      {/* e.g., Remote control status, sync status, etc. */}
    </View>
  );
}