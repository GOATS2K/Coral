import { useState } from 'react';
import { View, Image, Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { ListMusic, Play } from 'lucide-react-native';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { getArtistNames, getArtworkUrl } from '@/lib/player/player-format-utils';
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger,
} from '@/components/ui/context-menu';
import { TrackMenuItems } from '@/components/menu-items/track-menu-items';

interface PlayerQueueProps {
  queue: SimpleTrackDto[];
  currentIndex: number;
  reorderQueue: (fromIndex: number, toIndex: number) => void;
  playFromIndex: (index: number) => void;
}

export function PlayerQueue({
  queue,
  currentIndex,
  reorderQueue,
  playFromIndex,
}: PlayerQueueProps) {
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);

  const handleDragStart = (index: number) => (e: any) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragOver = (index: number) => (e: any) => {
    e.preventDefault();
    if (draggedIndex === null || draggedIndex === index) return;
    setDragOverIndex(index);
  };

  const handleDrop = (index: number) => (e: any) => {
    e.preventDefault();
    if (draggedIndex === null || draggedIndex === index) return;
    reorderQueue(draggedIndex, index);
    setDraggedIndex(null);
    setDragOverIndex(null);
  };

  const handleDragEnd = () => {
    setDraggedIndex(null);
    setDragOverIndex(null);
  };

  return (
    <>
      <Popover>
        <PopoverTrigger asChild>
          <Pressable className="web:hover:opacity-70 active:opacity-50">
            <ListMusic size={20} className="text-foreground" />
          </Pressable>
        </PopoverTrigger>
        <PopoverContent
          className="w-96 max-h-96 overflow-hidden p-0"
          align="end"
        >
          <View className="p-3 border-b border-border">
            <Text className="font-semibold">Queue ({queue.length})</Text>
          </View>
          <View className="overflow-y-auto max-h-80">
            {queue.map((track, index) => {
              const trackArtworkUrl = getArtworkUrl(track.album?.id);
              const trackArtists = getArtistNames(track);
              const isCurrentTrack = index === currentIndex;
              const isDraggedOver = dragOverIndex === index;
              const key = `${track.id}-${index}`;

              return (
                <ContextMenu key={key}>
                  <ContextMenuTrigger>
                    <div
                      draggable
                      onDragStart={handleDragStart(index)}
                      onDragOver={handleDragOver(index)}
                      onDrop={handleDrop(index)}
                      onDragEnd={handleDragEnd}
                      onMouseEnter={() => setHoveredIndex(index)}
                      onMouseLeave={() => setHoveredIndex(null)}
                      className={`flex flex-row items-center gap-3 p-2 cursor-grab active:cursor-grabbing ${isCurrentTrack ? 'bg-accent/50' : ''} ${isDraggedOver ? 'border-t-2 border-primary' : ''} hover:bg-accent/30 transition-colors`}
                      style={{
                        opacity: draggedIndex === index ? 0.5 : 1,
                        userSelect: 'none',
                        WebkitUserSelect: 'none',
                      }}
                    >
                      <div className="relative w-10 h-10">
                        {trackArtworkUrl ? (
                          <Image
                            source={{ uri: trackArtworkUrl }}
                            className="w-10 h-10 rounded"
                            resizeMode="cover"
                          />
                        ) : (
                          <View className="w-10 h-10 rounded bg-muted" />
                        )}
                        {hoveredIndex === index && (
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              playFromIndex(index);
                            }}
                            className="absolute inset-0 bg-black/60 rounded flex items-center justify-center hover:bg-black/70 transition-colors"
                          >
                            <Play size={16} className="text-white" fill="white" />
                          </button>
                        )}
                      </div>
                      <View className="flex-1 min-w-0">
                        <Text className="font-medium text-sm" numberOfLines={1}>
                          {track.title}
                        </Text>
                        <Text className="text-muted-foreground text-xs" numberOfLines={1}>
                          {trackArtists}
                        </Text>
                      </View>
                      {isCurrentTrack && (
                        <Text className="text-xs text-primary font-semibold">Playing</Text>
                      )}
                    </div>
                  </ContextMenuTrigger>

                  <ContextMenuContent className="w-56">
                    <TrackMenuItems
                      track={track}
                      components={{
                        MenuItem: ContextMenuItem,
                        MenuSub: ContextMenuSub,
                        MenuSubTrigger: ContextMenuSubTrigger,
                        MenuSubContent: ContextMenuSubContent,
                        MenuSeparator: ContextMenuSeparator,
                      }}
                      isQueueContext={true}
                    />
                  </ContextMenuContent>
                </ContextMenu>
              );
            })}
          </View>
        </PopoverContent>
      </Popover>
    </>
  );
}
