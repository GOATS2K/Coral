import React from 'react';
import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import { Progress } from '@/components/ui/progress';
import type { ScanProgressData } from '@/lib/signalr/signalr-service';

interface ScanProgressIndicatorProps {
  progress: ScanProgressData;
}

export function ScanProgressIndicator({ progress }: ScanProgressIndicatorProps) {
  // Calculate files processed and embedding progress
  const filesProcessed = progress.tracksAdded + progress.tracksUpdated;

  const embeddingProgress = filesProcessed > 0
    ? Math.round((progress.embeddingsCompleted / filesProcessed) * 100)
    : 0;

  // Progress bar shows total progress: tracks added + embeddings stored
  // expectedTracks * 2 because we need to add the track AND store its embedding
  const totalSteps = progress.expectedTracks * 2;
  const completedSteps = filesProcessed + progress.embeddingsCompleted;
  const scanProgress = totalSteps > 0
    ? Math.round((completedSteps / totalSteps) * 100)
    : undefined; // undefined = indeterminate

  // Format the library path to just show the folder name if it's too long
  const libraryPath = progress.libraryName;

  // Build status message parts
  const parts: string[] = [];

  // Check if this is a completion summary
  if (progress.isComplete) {
    parts.push(`Scan complete: ${libraryPath}`);
  } else {
    parts.push(`Scanning: ${libraryPath}`);
  }

  // Add change summary if there are any changes
  const changes: string[] = [];
  if (progress.tracksAdded > 0) changes.push(`Added: ${progress.tracksAdded}`);
  if (progress.tracksUpdated > 0) changes.push(`Updated: ${progress.tracksUpdated}`);
  if (progress.tracksDeleted > 0) changes.push(`Deleted: ${progress.tracksDeleted}`);

  if (changes.length > 0) {
    parts.push(changes.join(' • '));
  } else if (progress.isComplete) {
    parts.push('No changes');
  }

  // Add embedding progress if there are embeddings and not complete
  if (filesProcessed > 0 && !progress.isComplete) {
    parts.push(`Embeddings: ${progress.embeddingsCompleted}/${filesProcessed}`);
  }

  // Add overall progress percentage if not complete
  if (!progress.isComplete && scanProgress !== undefined) {
    parts.push(`${scanProgress}%`);
  }

  return (
    <View className="flex-col gap-1">
      <Text className="text-sm text-foreground">
        {parts.join(' • ')}
      </Text>
      {!progress.isComplete && (
        <Progress
          value={scanProgress}
          className="h-1"
          indicatorClassName="bg-primary"
        />
      )}
    </View>
  );
}