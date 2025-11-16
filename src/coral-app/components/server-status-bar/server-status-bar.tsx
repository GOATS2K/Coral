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
    <View className="transition-all duration-300 ease-in-out">
      {/* Horizontal rule separator */}
      <View className="mx-4 mb-2 border-t border-border" />

      <View className="mx-4 mb-4 py-2 px-4">
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
    </View>
  );
}