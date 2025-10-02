import { useState, useEffect } from 'react';
import { View, Pressable } from 'react-native';
import { Volume2, VolumeX } from 'lucide-react-native';

interface PlayerVolumeProps {
  volume: number;
  isMuted: boolean;
  setVolume: (volume: number) => void;
  toggleMute: () => void;
}

export function PlayerVolume({ volume, isMuted, setVolume, toggleMute }: PlayerVolumeProps) {
  const [localVolume, setLocalVolume] = useState(volume);

  useEffect(() => {
    setLocalVolume(volume);
  }, [volume]);

  const handleVolumeChange = (e: any) => {
    const newVolume = parseFloat(e.target.value);
    setLocalVolume(newVolume);
    setVolume(newVolume);
  };

  return (
    <View className="flex-row items-center gap-2">
      <Pressable onPress={toggleMute} className="web:hover:opacity-70 active:opacity-50">
        {isMuted ? (
          <VolumeX size={20} className="text-muted-foreground" />
        ) : (
          <Volume2 size={20} className="text-foreground" />
        )}
      </Pressable>
      <input
        type="range"
        min={0}
        max={1}
        step={0.01}
        value={localVolume}
        onChange={handleVolumeChange}
        className={`w-24 h-1 accent-primary cursor-pointer appearance-none rounded-full [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3 [&::-webkit-slider-thumb]:h-3 [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-primary [&::-webkit-slider-thumb]:opacity-0 [&::-webkit-slider-thumb]:transition-opacity hover:[&::-webkit-slider-thumb]:opacity-100 [&::-moz-range-thumb]:w-3 [&::-moz-range-thumb]:h-3 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-primary [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:opacity-0 [&::-moz-range-thumb]:transition-opacity hover:[&::-moz-range-thumb]:opacity-100 ${isMuted ? 'opacity-40' : ''}`}
        style={{
          background: `linear-gradient(to right, hsl(var(--primary)) 0%, hsl(var(--primary)) ${localVolume * 100}%, hsl(var(--muted)) ${localVolume * 100}%, hsl(var(--muted)) 100%)`,
        }}
      />
    </View>
  );
}
