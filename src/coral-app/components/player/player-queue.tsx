import { useState, useCallback, memo, useRef, useEffect } from 'react';
import { View, Image, Pressable } from 'react-native';
import { Text } from '@/components/ui/text';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { ListMusic, Play } from 'lucide-react-native';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { getArtistNames, getArtworkUrl } from '@/lib/player/player-format-utils';
import { Icon } from '@/components/ui/icon';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
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

interface QueueItemProps {
  track: SimpleTrackDto;
  index: number;
  draggedIndex: number | null;
  isCurrentTrack: boolean;
  isDragged: boolean;
  isDraggedOver: boolean;
  isHovered: boolean;
  onDragStart: (index: number, e: any) => void;
  onDragOver: (index: number, draggedIdx: number | null, e: any) => void;
  onDrop: (index: number, draggedIdx: number | null, e: any) => void;
  onDragEnd: () => void;
  onMouseEnter: (index: number) => void;
  onMouseLeave: () => void;
  onPlay: (index: number) => void;
}

const QueueItem = memo(function QueueItem({
  track,
  index,
  draggedIndex,
  isCurrentTrack,
  isDragged,
  isDraggedOver,
  isHovered,
  onDragStart,
  onDragOver,
  onDrop,
  onDragEnd,
  onMouseEnter,
  onMouseLeave,
  onPlay,
}: QueueItemProps) {
  const trackArtworkUrl = getArtworkUrl(track.album?.id);
  const trackArtists = getArtistNames(track);
  const [imageError, setImageError] = useState(false);

  // Reset image error state when track changes
  useEffect(() => {
    setImageError(false);
  }, [track.id]);

  // Create bound handlers inside component to avoid creating new functions in parent
  const handleDragStart = useCallback((e: any) => {
    onDragStart(index, e);
  }, [index, onDragStart]);

  const handleDragOver = useCallback((e: any) => {
    onDragOver(index, draggedIndex, e);
  }, [index, draggedIndex, onDragOver]);

  const handleDrop = useCallback((e: any) => {
    onDrop(index, draggedIndex, e);
  }, [index, draggedIndex, onDrop]);

  const handleMouseEnter = useCallback(() => {
    onMouseEnter(index);
  }, [index, onMouseEnter]);

  const handlePlay = useCallback(() => {
    onPlay(index);
  }, [index, onPlay]);

  return (
    <ContextMenu>
      <ContextMenuTrigger>
        <div
          draggable={true}
          onDragStart={handleDragStart}
          onDragOver={handleDragOver}
          onDrop={handleDrop}
          onDragEnd={onDragEnd}
          onMouseEnter={handleMouseEnter}
          onMouseLeave={onMouseLeave}
          className={`flex flex-row items-center gap-3 p-2 cursor-grab active:cursor-grabbing ${isCurrentTrack ? 'bg-accent/50' : ''} ${isDraggedOver ? 'border-t-2 border-primary' : ''} hover:bg-accent/30 transition-colors`}
          style={{
            opacity: isDragged ? 0.5 : 1,
            userSelect: 'none',
            WebkitUserSelect: 'none',
          }}
        >
          <div className="relative w-10 h-10">
            {trackArtworkUrl && !imageError ? (
              <Image
                source={{ uri: trackArtworkUrl }}
                className="w-10 h-10 rounded"
                resizeMode="cover"
                onError={() => setImageError(true)}
              />
            ) : (
              <View className="w-10 h-10 rounded overflow-hidden">
                <MissingAlbumCover size={16} />
              </View>
            )}
            {isHovered && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handlePlay();
                }}
                className="absolute inset-0 bg-black/60 rounded flex items-center justify-center hover:bg-black/70 transition-colors"
              >
                <Icon as={Play} size={16} className="text-white" fill="currentColor" />
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
});

export function PlayerQueue({
  queue,
  currentIndex,
  reorderQueue,
  playFromIndex,
}: PlayerQueueProps) {
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const scrollIntervalRef = useRef<number | null>(null);

  const handleDragStart = useCallback((index: number, e: any) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', String(index));
  }, []);

  const handleDragOver = useCallback((index: number, draggedIdx: number | null, e: any) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    if (draggedIdx === null || draggedIdx === index) return;
    setDragOverIndex(index);
  }, []);

  const handleDrop = useCallback((index: number, draggedIdx: number | null, e: any) => {
    e.preventDefault();
    if (draggedIdx === null || draggedIdx === index) return;

    // The visual line appears above the item at 'index'
    // This means the user wants to insert the dragged item BEFORE the item at 'index'
    // However, we need to account for index shifting when moving items
    let targetIndex = index;

    // If dragging from above to below, the removal shifts indices
    // so we need to adjust the target
    if (draggedIdx < index) {
      targetIndex = index - 1;
    }

    reorderQueue(draggedIdx, targetIndex);
    setDraggedIndex(null);
    setDragOverIndex(null);
  }, [reorderQueue]);

  const handleDragEnd = useCallback(() => {
    setDraggedIndex(null);
    setDragOverIndex(null);
  }, []);

  const handleMouseEnter = useCallback((index: number) => {
    setHoveredIndex(index);
  }, []);

  const handleMouseLeave = useCallback(() => {
    setHoveredIndex(null);
  }, []);

  const handlePlay = useCallback((index: number) => {
    playFromIndex(index);
  }, [playFromIndex]);

  // Auto-scroll when dragging near edges
  const handleContainerDragOver = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    if (draggedIndex === null || !scrollContainerRef.current) return;

    const container = scrollContainerRef.current;
    const rect = container.getBoundingClientRect();
    const mouseY = e.clientY - rect.top;
    const scrollThreshold = 100; // pixels from edge to trigger scroll
    const maxScrollSpeed = 25; // pixels per frame

    // Clear existing interval
    if (scrollIntervalRef.current) {
      clearInterval(scrollIntervalRef.current);
      scrollIntervalRef.current = null;
    }

    // Calculate scroll speed based on proximity to edges
    let scrollSpeed = 0;

    if (mouseY < scrollThreshold) {
      // Near top - scroll up
      const proximity = 1 - (mouseY / scrollThreshold);
      scrollSpeed = -proximity * maxScrollSpeed;
    } else if (mouseY > rect.height - scrollThreshold) {
      // Near bottom - scroll down
      const proximity = 1 - ((rect.height - mouseY) / scrollThreshold);
      scrollSpeed = proximity * maxScrollSpeed;
    }

    if (scrollSpeed !== 0) {
      scrollIntervalRef.current = window.setInterval(() => {
        if (scrollContainerRef.current) {
          scrollContainerRef.current.scrollTop += scrollSpeed;
        }
      }, 16); // ~60fps
    }
  }, [draggedIndex]);

  // Clean up scroll interval on drag end
  useEffect(() => {
    if (draggedIndex === null && scrollIntervalRef.current) {
      clearInterval(scrollIntervalRef.current);
      scrollIntervalRef.current = null;
    }
  }, [draggedIndex]);

  // Clean up on unmount
  useEffect(() => {
    return () => {
      if (scrollIntervalRef.current) {
        clearInterval(scrollIntervalRef.current);
      }
    };
  }, []);

  return (
    <>
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
            ref={scrollContainerRef}
            onDragOver={handleContainerDragOver}
            className="overflow-y-auto max-h-80"
            style={{
              scrollbarWidth: 'none', // Firefox
              msOverflowStyle: 'none', // IE and Edge
              WebkitOverflowScrolling: 'touch',
            }}
          >
            {queue.map((track, index) => {
              const isCurrentTrack = index === currentIndex;
              const isDragged = draggedIndex === index;
              const isDraggedOver = dragOverIndex === index;
              const isHovered = hoveredIndex === index;
              // Combine track ID and index for safe key (same song can appear multiple times)
              const key = `${track.id}-${index}`;

              return (
                <QueueItem
                  key={key}
                  track={track}
                  index={index}
                  draggedIndex={draggedIndex}
                  isCurrentTrack={isCurrentTrack}
                  isDragged={isDragged}
                  isDraggedOver={isDraggedOver}
                  isHovered={isHovered}
                  onDragStart={handleDragStart}
                  onDragOver={handleDragOver}
                  onDrop={handleDrop}
                  onDragEnd={handleDragEnd}
                  onMouseEnter={handleMouseEnter}
                  onMouseLeave={handleMouseLeave}
                  onPlay={handlePlay}
                />
              );
            })}
          </div>
        </PopoverContent>
      </Popover>
    </>
  );
}
