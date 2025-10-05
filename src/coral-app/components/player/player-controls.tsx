import { View, Pressable } from 'react-native';
import { Play, Pause, SkipBack, SkipForward, Shuffle, Repeat, Repeat1 } from 'lucide-react-native';
import type { RepeatMode } from '@/lib/state';
import { Icon } from '@/components/ui/icon';

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
        <Icon as={Shuffle} size={20} className={isShuffled ? "text-primary" : "text-muted-foreground"} />
      </Pressable>
      <Pressable onPress={() => skip(-1)} className="web:hover:opacity-70 active:opacity-50">
        <Icon as={SkipBack} size={20} className="text-foreground" />
      </Pressable>
      <Pressable onPress={togglePlayPause} className="web:hover:opacity-70 active:opacity-50 bg-primary rounded-full p-2">
        {isPlaying ? (
          <Icon as={Pause} size={20} className="text-primary-foreground" fill="currentColor" />
        ) : (
          <Icon as={Play} size={20} className="text-primary-foreground" fill="currentColor" />
        )}
      </Pressable>
      <Pressable onPress={() => skip(1)} className="web:hover:opacity-70 active:opacity-50">
        <Icon as={SkipForward} size={20} className="text-foreground" />
      </Pressable>
      <Pressable onPress={cycleRepeat} className="web:hover:opacity-70 active:opacity-50">
        {repeat === 'one' ? (
          <Icon as={Repeat1} size={20} className="text-primary" />
        ) : (
          <Icon as={Repeat} size={20} className={repeat === 'all' ? "text-primary" : "text-muted-foreground"} />
        )}
      </Pressable>
    </View>
  );
}
