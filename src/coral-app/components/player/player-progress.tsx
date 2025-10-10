import { useState } from 'react';
import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import { formatTime } from '@/lib/player/player-format-utils';

interface PlayerProgressProps {
  position: number;
  duration: number;
  seekTo: (position: number) => void;
  isBuffering?: boolean;
}

export function PlayerProgress({ position, duration, seekTo, isBuffering = false }: PlayerProgressProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [dragPosition, setDragPosition] = useState(0);

  const currentPosition = isDragging ? dragPosition : position;

  const handleSeekChange = (e: any) => {
    setDragPosition(parseFloat(e.target.value));
  };

  const handleSeekStart = () => {
    setIsDragging(true);
  };

  const handleSeekEnd = (e: any) => {
    const newPosition = parseFloat(e.target.value);
    setIsDragging(false);
    seekTo(newPosition);
  };

  return (
    <View className="flex-row items-center gap-2">
      <Text className="text-xs text-muted-foreground w-10 text-right select-none">
        {formatTime(currentPosition)}
      </Text>
      {isBuffering ? (
        <View className="flex-1 h-1 bg-muted rounded-full overflow-hidden relative">
          <View
            className="absolute h-full w-1/3 bg-primary/60"
            style={{
              animation: 'indeterminate 1.5s ease-in-out infinite',
            }}
          />
          <style>{`
            @keyframes indeterminate {
              0% { left: -33.33%; }
              50% { left: 100%; }
              100% { left: 100%; }
            }
          `}</style>
        </View>
      ) : (
        <input
          type="range"
          min={0}
          max={duration}
          value={currentPosition}
          onChange={handleSeekChange}
          onMouseDown={handleSeekStart}
          onMouseUp={handleSeekEnd}
          onTouchStart={handleSeekStart}
          onTouchEnd={handleSeekEnd}
          className="flex-1 h-1 accent-primary cursor-pointer appearance-none rounded-full [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3 [&::-webkit-slider-thumb]:h-3 [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-primary [&::-webkit-slider-thumb]:opacity-0 [&::-webkit-slider-thumb]:transition-opacity hover:[&::-webkit-slider-thumb]:opacity-100 [&::-moz-range-thumb]:w-3 [&::-moz-range-thumb]:h-3 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-primary [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:opacity-0 [&::-moz-range-thumb]:transition-opacity hover:[&::-moz-range-thumb]:opacity-100"
          style={{
            background: `linear-gradient(to right, hsl(var(--primary)) 0%, hsl(var(--primary)) ${(currentPosition / duration) * 100}%, hsl(var(--muted)) ${(currentPosition / duration) * 100}%, hsl(var(--muted)) 100%)`,
          }}
        />
      )}
      <Text className="text-xs text-muted-foreground w-10 select-none">
        {formatTime(duration)}
      </Text>
    </View>
  );
}
