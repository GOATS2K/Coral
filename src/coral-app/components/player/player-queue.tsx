import { View, Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { ListMusic } from 'lucide-react-native';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { Icon } from '@/components/ui/icon';
import { ReorderableTrackListing, ReorderableTrack } from '@/components/reorderable-track-listing';

interface PlayerQueueProps {
  queue: SimpleTrackDto[];
  reorderQueue: (fromIndex: number, toIndex: number) => void;
  playFromIndex: (index: number) => void;
}

export function PlayerQueue({
  queue,
  reorderQueue,
  playFromIndex,
}: PlayerQueueProps) {
  // Transform SimpleTrackDto[] to ReorderableTrack[]
  // Use track.id + index for unique key since same track can appear multiple times
  const reorderableTracks: ReorderableTrack[] = queue.map((track, index) => ({
    id: `${track.id}-${index}`,
    track,
  }));

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Pressable className="web:hover:opacity-70 active:opacity-50">
          <Icon as={ListMusic} size={20} className="text-foreground" />
        </Pressable>
      </PopoverTrigger>
      <PopoverContent
        className="w-96 max-h-96 overflow-hidden p-0"
        align="end"
      >
        <View className="p-3 border-b border-border">
          <Text className="font-semibold">Queue ({queue.length})</Text>
        </View>
        <div
          className="overflow-y-auto max-h-80"
          style={{
            scrollbarWidth: 'none',
            msOverflowStyle: 'none',
            WebkitOverflowScrolling: 'touch',
          }}
        >
          <ReorderableTrackListing
            tracks={reorderableTracks}
            onReorder={reorderQueue}
            onPlay={playFromIndex}
            compact
          />
        </div>
      </PopoverContent>
    </Popover>
  );
}
