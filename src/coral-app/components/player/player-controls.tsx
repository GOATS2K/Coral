import { View, Pressable } from 'react-native';
import { Play, Pause, SkipBack, SkipForward, Shuffle, Repeat, Repeat1 } from 'lucide-react-native';
import type { RepeatMode } from '@/lib/state';

interface PlayerControlsProps {
  isPlaying: boolean;
  repeat: RepeatMode;
  isShuffled: boolean;
  togglePlayPause: () => void;
  skip: (direction: 1 | -1) => void;
  shuffle: () => void;
  cycleRepeat: () => void;
}

export function PlayerControls({
  isPlaying,
  repeat,
  isShuffled,
  togglePlayPause,
  skip,
  shuffle,
  cycleRepeat,
}: PlayerControlsProps) {
  return (
    <View className="flex-row items-center justify-center gap-4">
      <Pressable onPress={shuffle} className="web:hover:opacity-70 active:opacity-50">
        <Shuffle size={20} className={isShuffled ? "text-primary" : "text-muted-foreground"} />
      </Pressable>
      <Pressable onPress={() => skip(-1)} className="web:hover:opacity-70 active:opacity-50">
        <SkipBack size={20} className="text-foreground" />
      </Pressable>
      <Pressable onPress={togglePlayPause} className="web:hover:opacity-70 active:opacity-50 bg-primary rounded-full p-2">
        {isPlaying ? (
          <Pause size={20} className="text-primary-foreground" fill="currentColor" />
        ) : (
          <Play size={20} className="text-primary-foreground" fill="currentColor" />
        )}
      </Pressable>
      <Pressable onPress={() => skip(1)} className="web:hover:opacity-70 active:opacity-50">
        <SkipForward size={20} className="text-foreground" />
      </Pressable>
      <Pressable onPress={cycleRepeat} className="web:hover:opacity-70 active:opacity-50">
        {repeat === 'one' ? (
          <Repeat1 size={20} className="text-primary" />
        ) : (
          <Repeat size={20} className={repeat === 'all' ? "text-primary" : "text-muted-foreground"} />
        )}
      </Pressable>
    </View>
  );
}
