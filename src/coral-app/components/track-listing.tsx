import { Pressable, View, Platform } from 'react-native';
import { Text } from '@/components/ui/text';
import { SimpleTrackDto, AlbumDto } from '@/lib/client/schemas';
import { usePlayer } from '@/lib/player/use-player';
import { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Sparkles, Plus } from 'lucide-react-native';
import { useToast } from '@/lib/hooks/use-toast';

interface TrackListingProps {
  tracks: SimpleTrackDto[];
  album?: AlbumDto;
  showTrackNumber?: boolean;
  className?: string;
}

export function TrackListing({ tracks, album, showTrackNumber = true, className }: TrackListingProps) {
  const { play, activeTrack, addToQueue, findSimilarAndAddToQueue } = usePlayer();
  const { showToast } = useToast();
  const [contextMenu, setContextMenu] = useState<{ trackId: string; x: number; y: number } | null>(null);

  const hasMultipleDiscs = new Set(tracks.map(t => t.discNumber || 1)).size > 1;

  const formatDuration = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  const formatTrackNumber = (track: SimpleTrackDto) => {
    const num = track.trackNumber.toString().padStart(2, '0');
    return hasMultipleDiscs ? `${track.discNumber || 1}.${num}` : num;
  };

  const handleContextMenu = (trackId: string) => (e: any) => {
    if (Platform.OS !== 'web') return;
    e.preventDefault();
    setContextMenu({ trackId, x: e.clientX, y: e.clientY });
  };

  const handleFindSimilar = async (trackId: string) => {
    await findSimilarAndAddToQueue(trackId);
    setContextMenu(null);
  };

  const handleAddToQueue = (track: SimpleTrackDto) => {
    addToQueue(track);
    showToast(`Added "${track.title}" to queue`);
    setContextMenu(null);
  };

  // Close context menu when clicking outside
  useEffect(() => {
    if (Platform.OS !== 'web') return;
    const handleClick = () => setContextMenu(null);
    if (contextMenu) {
      window.addEventListener('click', handleClick);
      return () => window.removeEventListener('click', handleClick);
    }
  }, [contextMenu]);

  return (
    <>
      <View className={className}>
        {tracks.map((track, index) => {
          const isActive = activeTrack?.id === track.id;
          return (
            <Pressable
              key={track.id}
              onPress={() => play(tracks, index)}
              // @ts-ignore - web-only prop
              onContextMenu={handleContextMenu(track.id)}
              className={`flex-row py-2 items-center gap-2 web:cursor-pointer active:bg-muted/50 web:hover:bg-muted/30 rounded-md -mx-2 px-2 ${isActive ? 'bg-primary/10' : ''}`}
            >
              {showTrackNumber && (
                <Text variant="small" className={`w-8 select-none text-xs ${isActive ? 'text-primary font-medium' : 'text-muted-foreground'}`}>
                  {formatTrackNumber(track)}
                </Text>
              )}
              <View className="flex-1 min-w-0">
                <Text variant="default" className={`select-none leading-tight text-sm ${isActive ? 'text-primary font-medium' : 'text-foreground'}`} numberOfLines={1}>
                  {track.title}
                </Text>
                <Text variant="small" className={`mt-0.5 select-none leading-tight text-xs ${isActive ? 'text-primary/80' : 'text-muted-foreground'}`} numberOfLines={1}>
                  {track.artists.filter(a => a.role === 'Main').map(a => a.name).join(', ')}
                </Text>
              </View>
              <Text variant="small" className={`hidden sm:block w-12 text-right select-none text-xs ${isActive ? 'text-primary font-medium' : 'text-muted-foreground'}`}>
                {formatDuration(track.durationInSeconds)}
              </Text>
            </Pressable>
          );
        })}
      </View>

      {/* Context Menu - rendered via portal */}
      {Platform.OS === 'web' && contextMenu && typeof document !== 'undefined' && createPortal(
        <div
          className="fixed bg-popover border border-border rounded-md shadow-lg py-1 z-50"
          style={{
            left: contextMenu.x,
            top: contextMenu.y,
          }}
          onClick={(e) => e.stopPropagation()}
        >
          <button
            className="w-full px-4 py-2 text-left text-sm hover:bg-accent transition-colors flex items-center gap-2"
            onClick={() => handleFindSimilar(contextMenu.trackId)}
          >
            <Sparkles size={14} className="text-foreground" />
            <span className="text-foreground">Find similar songs</span>
          </button>
          <button
            className="w-full px-4 py-2 text-left text-sm hover:bg-accent transition-colors flex items-center gap-2"
            onClick={() => {
              const track = tracks.find(t => t.id === contextMenu.trackId);
              if (track) handleAddToQueue(track);
            }}
          >
            <Plus size={14} className="text-foreground" />
            <span className="text-foreground">Add to queue</span>
          </button>
        </div>,
        document.body
      )}
    </>
  );
}