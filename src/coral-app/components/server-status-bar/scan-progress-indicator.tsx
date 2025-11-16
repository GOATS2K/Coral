import React from 'react';
import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import type { ScanProgressData } from '@/lib/signalr/signalr-service';

interface ScanProgressIndicatorProps {
  progress: ScanProgressData;
}

export function ScanProgressIndicator({ progress }: ScanProgressIndicatorProps) {
  // Calculate total progress percentage (average of indexing and embedding progress)
  const totalProgress = progress.tracksIndexed > 0
    ? Math.round((progress.embeddingsCompleted / progress.tracksIndexed) * 100)
    : 0;

  // Format the library path to just show the folder name if it's too long
  const libraryPath = progress.libraryName;

  return (
    <View className="flex-row items-center justify-start">
      <Text className="text-sm text-foreground">
        Scanning: {libraryPath} • {totalProgress}% • Indexed: {progress.tracksIndexed} / {progress.tracksIndexed} • Embeddings: {progress.embeddingsCompleted} / {progress.tracksIndexed}
      </Text>
    </View>
  );
}