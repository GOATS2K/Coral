import { useState, useCallback, useRef, useEffect } from 'react';
import { View, Image, Platform } from 'react-native';
import { Text } from '@/components/ui/text';
import { Play } from 'lucide-react-native';
import type { SimpleTrackDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';
import { Icon } from '@/components/ui/icon';
import { MissingAlbumCover } from '@/components/ui/missing-album-cover';
import { usePlayerActions } from '@/lib/player/use-player';
import { PlaybackInitializer, playerStateAtom } from '@/lib/state';
import { useAtomValue } from 'jotai';
import { TrackMenu } from '@/components/track-menu';
import { formatTime } from '@/lib/player/player-format-utils';

export interface ReorderableTrack {
  id: string;
  track: SimpleTrackDto;
}

interface ReorderableTrackListingProps {
  tracks: ReorderableTrack[];
  onReorder: (fromIndex: number, toIndex: number) => void;
  onPlay?: (index: number) => void;  // Override default play behavior
  className?: string;
  initializer?: PlaybackInitializer;
  compact?: boolean;  // Use compact padding (for popovers)
  showTrackNumber?: boolean;  // Show playlist position number alongside artwork
}

interface ReorderableTrackRowProps {
  item: ReorderableTrack;
  index: number;
  isActive: boolean;
  isDragging: boolean;
  isDropTarget: boolean;
  isHovered: boolean;
  compact: boolean;
  showTrackNumber: boolean;
  onPlay: (index: number) => void;
  onDragStart: (index: number, e: React.DragEvent) => void;
  onDragOver: (index: number, e: React.DragEvent) => void;
  onDrop: (index: number, e: React.DragEvent) => void;
  onDragEnd: () => void;
  onMouseEnter: (index: number) => void;
  onMouseLeave: () => void;
}

function ReorderableTrackRow({
  item,
  index,
  isActive,
  isDragging,
  isDropTarget,
  isHovered,
  compact,
  showTrackNumber,
  onPlay,
  onDragStart,
  onDragOver,
  onDrop,
  onDragEnd,
  onMouseEnter,
  onMouseLeave,
}: ReorderableTrackRowProps) {
  const { track } = item;
  const [imageError, setImageError] = useState(false);
  const artworkUrl = track.album?.id
    ? `${baseUrl}/api/artwork?albumId=${track.album.id}&size=small`
    : null;

  useEffect(() => {
    setImageError(false);
  }, [track.id]);

  const handleDragStart = useCallback(
    (e: React.DragEvent) => onDragStart(index, e),
    [index, onDragStart]
  );

  const handleDragOver = useCallback(
    (e: React.DragEvent) => onDragOver(index, e),
    [index, onDragOver]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => onDrop(index, e),
    [index, onDrop]
  );

  const handleMouseEnter = useCallback(
    () => onMouseEnter(index),
    [index, onMouseEnter]
  );

  const handlePlay = useCallback(() => onPlay(index), [index, onPlay]);

  const artistNames = track.artists
    .filter((a) => a.role === 'Main')
    .map((a) => a.name)
    .join(', ');

  const isWeb = Platform.OS === 'web';

  const rowContent = (
    <div
      draggable={isWeb}
      onDragStart={isWeb ? handleDragStart : undefined}
      onDragOver={isWeb ? handleDragOver : undefined}
      onDrop={isWeb ? handleDrop : undefined}
      onDragEnd={isWeb ? onDragEnd : undefined}
      onMouseEnter={isWeb ? handleMouseEnter : undefined}
      onMouseLeave={isWeb ? onMouseLeave : undefined}
      onDoubleClick={isWeb ? handlePlay : undefined}
      className={`flex flex-row items-center gap-2 py-2 cursor-grab active:cursor-grabbing hover:bg-muted/30 active:bg-muted/50 transition-colors ${compact ? 'px-3' : '-mx-4 px-4 sm:-mx-6 sm:px-6'} ${isActive ? 'bg-accent/30' : ''} ${isDropTarget ? 'border-t-2 border-primary' : ''}`}
      style={{
        opacity: isDragging ? 0.5 : 1,
        userSelect: 'none',
        WebkitUserSelect: 'none',
      }}
    >
      {/* Track number (playlist position) */}
      {showTrackNumber && (
        <Text
          className={`w-8 text-xs select-none ${
            isActive ? 'text-orange-700 dark:text-orange-500 font-bold' : 'text-muted-foreground'
          }`}
        >
          {(index + 1).toString().padStart(2, '0')}
        </Text>
      )}

      {/* Artwork with play overlay on hover */}
      <div className="relative w-10 h-10 rounded overflow-hidden">
        {artworkUrl && !imageError ? (
          <Image
            source={{ uri: artworkUrl }}
            className="w-full h-full"
            resizeMode="cover"
            onError={() => setImageError(true)}
          />
        ) : (
          <MissingAlbumCover size={16} />
        )}
        {isHovered && (
          <button
            onClick={(e) => {
              e.stopPropagation();
              handlePlay();
            }}
            className="absolute inset-0 bg-black/60 flex items-center justify-center hover:bg-black/70 transition-colors"
          >
            <Icon as={Play} size={16} className="text-white" fill="currentColor" />
          </button>
        )}
      </div>

      {/* Track info */}
      <View className="flex-1 min-w-0">
        <Text
          className={`text-sm leading-tight ${
            isActive
              ? 'text-orange-700 dark:text-orange-500 font-bold'
              : 'text-foreground'
          }`}
          numberOfLines={1}
        >
          {track.title}
        </Text>
        <Text
          className={`text-xs leading-tight mt-0.5 ${
            isActive
              ? 'text-orange-700/80 dark:text-orange-500/80 font-semibold'
              : 'text-muted-foreground'
          }`}
          numberOfLines={1}
        >
          {artistNames}
        </Text>
      </View>

      {/* Duration */}
      <Text
        className={`hidden sm:block text-xs w-12 text-right ${
          isActive
            ? 'text-orange-700 dark:text-orange-500 font-bold'
            : 'text-muted-foreground'
        }`}
      >
        {formatTime(track.durationInSeconds)}
      </Text>
    </div>
  );

  return (
    <TrackMenu track={track} isQueueContext>
      {rowContent}
    </TrackMenu>
  );
}

export function ReorderableTrackListing({
  tracks,
  onReorder,
  onPlay: onPlayOverride,
  className,
  initializer,
  compact = false,
  showTrackNumber = false,
}: ReorderableTrackListingProps) {
  const { play } = usePlayerActions();
  const activeTrack = useAtomValue(playerStateAtom).currentTrack;

  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const scrollIntervalRef = useRef<number | null>(null);

  const handleDragStart = useCallback((index: number, e: React.DragEvent) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', String(index));
  }, []);

  const handleDragOver = useCallback(
    (index: number, e: React.DragEvent) => {
      e.preventDefault();
      e.dataTransfer.dropEffect = 'move';
      if (draggedIndex === null || draggedIndex === index) return;
      setDragOverIndex(index);
    },
    [draggedIndex]
  );

  const handleDrop = useCallback(
    (index: number, e: React.DragEvent) => {
      e.preventDefault();
      if (draggedIndex === null || draggedIndex === index) return;

      let targetIndex = index;
      if (draggedIndex < index) {
        targetIndex = index - 1;
      }

      onReorder(draggedIndex, targetIndex);
      setDraggedIndex(null);
      setDragOverIndex(null);
    },
    [draggedIndex, onReorder]
  );

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

  const handlePlay = useCallback(
    (index: number) => {
      if (onPlayOverride) {
        onPlayOverride(index);
      } else {
        const simpleTracks = tracks.map((t) => t.track);
        play(simpleTracks, index, initializer);
      }
    },
    [tracks, play, initializer, onPlayOverride]
  );

  // Find the closest scrollable ancestor
  const getScrollableParent = useCallback((element: HTMLElement | null): HTMLElement | null => {
    if (!element) return null;

    let current: HTMLElement | null = element;
    while (current) {
      const style = getComputedStyle(current);
      const overflowY = style.overflowY;
      if ((overflowY === 'auto' || overflowY === 'scroll') && current.scrollHeight > current.clientHeight) {
        return current;
      }
      current = current.parentElement;
    }
    return null;
  }, []);

  // Auto-scroll when dragging near edges
  const handleContainerDragOver = useCallback(
    (e: React.DragEvent<HTMLDivElement>) => {
      if (draggedIndex === null || !containerRef.current) return;

      // Prevent the "no-drop" cursor when dragging within the container
      e.preventDefault();
      e.dataTransfer.dropEffect = 'move';

      const scrollable = getScrollableParent(containerRef.current) || containerRef.current;
      const rect = scrollable.getBoundingClientRect();
      const mouseY = e.clientY - rect.top;
      const scrollThreshold = 100;
      const maxScrollSpeed = 25;

      if (scrollIntervalRef.current) {
        clearInterval(scrollIntervalRef.current);
        scrollIntervalRef.current = null;
      }

      let scrollSpeed = 0;
      if (mouseY < scrollThreshold) {
        const proximity = 1 - mouseY / scrollThreshold;
        scrollSpeed = -proximity * maxScrollSpeed;
      } else if (mouseY > rect.height - scrollThreshold) {
        const proximity = 1 - (rect.height - mouseY) / scrollThreshold;
        scrollSpeed = proximity * maxScrollSpeed;
      }

      if (scrollSpeed !== 0) {
        scrollIntervalRef.current = window.setInterval(() => {
          scrollable.scrollTop += scrollSpeed;
        }, 16);
      }
    },
    [draggedIndex, getScrollableParent]
  );

  useEffect(() => {
    if (draggedIndex === null && scrollIntervalRef.current) {
      clearInterval(scrollIntervalRef.current);
      scrollIntervalRef.current = null;
    }
  }, [draggedIndex]);

  // Prevent "no-drop" cursor when dragging outside the container
  useEffect(() => {
    if (draggedIndex === null) return;

    const handleGlobalDragOver = (e: DragEvent) => {
      e.preventDefault();
      e.dataTransfer!.dropEffect = 'move';
    };

    document.addEventListener('dragover', handleGlobalDragOver);
    return () => document.removeEventListener('dragover', handleGlobalDragOver);
  }, [draggedIndex]);

  useEffect(() => {
    return () => {
      if (scrollIntervalRef.current) {
        clearInterval(scrollIntervalRef.current);
      }
    };
  }, []);

  return (
    <div
      ref={containerRef}
      onDragOver={handleContainerDragOver}
      className={className}
    >
      {tracks.map((item, index) => {
        const isActive = activeTrack?.id === item.track.id;
        const isDragging = draggedIndex === index;
        const isDropTarget = dragOverIndex === index;
        const isHovered = hoveredIndex === index;

        return (
          <ReorderableTrackRow
            key={item.id}
            item={item}
            index={index}
            isActive={isActive}
            isDragging={isDragging}
            isDropTarget={isDropTarget}
            isHovered={isHovered}
            compact={compact}
            showTrackNumber={showTrackNumber}
            onPlay={handlePlay}
            onDragStart={handleDragStart}
            onDragOver={handleDragOver}
            onDrop={handleDrop}
            onDragEnd={handleDragEnd}
            onMouseEnter={handleMouseEnter}
            onMouseLeave={handleMouseLeave}
          />
        );
      })}
    </div>
  );
}
