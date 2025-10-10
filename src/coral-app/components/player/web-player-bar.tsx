import { Platform, View } from 'react-native';
import { usePlayer } from '@/lib/player/use-player';
import { useMediaSession } from '@/lib/player/use-media-session';
import { PlayerTrackInfo } from './player-track-info';
import { PlayerControls } from './player-controls';
import { PlayerProgress } from './player-progress';
import { PlayerQueue } from './player-queue';
import { PlayerVolume } from './player-volume';
import { useAtomValue, useSetAtom } from 'jotai';
import { playerStateAtom } from '@/lib/state';

export function WebPlayerBar() {
  // Hooks must be called unconditionally, before any early returns
  const {
    activeTrack,
    isPlaying,
    progress,
    volume,
    isMuted,
    queue,
    currentIndex,
    repeat,
    isShuffled,
    togglePlayPause,
    skip,
    seekTo,
    setVolume,
    toggleMute,
    playFromIndex,
  } = usePlayer();

  const setState = useSetAtom(playerStateAtom);
  const playerState = useAtomValue(playerStateAtom);

  useMediaSession({ activeTrack, isPlaying, progress, togglePlayPause, skip, seekTo });

  // Early returns after all hooks
  if (Platform.OS !== 'web') {
    return null;
  }

  if (!activeTrack) {
    return null;
  }

  const duration = progress.duration || activeTrack.durationInSeconds || 0;

  return (
    <View className="bg-card border-t border-border h-24 flex-row items-center px-4 py-3 gap-4">
      {/* Album Art & Track Info */}
      <PlayerTrackInfo track={activeTrack} initializer={playerState.initializer} />

      {/* Playback Controls & Progress */}
      <View className="flex-1 max-w-2xl flex-col gap-2">
        <PlayerControls
          isPlaying={isPlaying}
          repeat={repeat}
          isShuffled={isShuffled}
          togglePlayPause={togglePlayPause}
          skip={skip}
          shuffle={() => setState({ type: 'shuffle' })}
          cycleRepeat={() => setState({ type: 'cycleRepeat' })}
        />
        <PlayerProgress position={progress.position} duration={duration} seekTo={seekTo} />
      </View>

      {/* Queue & Volume Controls */}
      <View className="flex-row items-center gap-5 flex-1 justify-end">
        <PlayerQueue
          queue={queue}
          currentIndex={currentIndex}
          reorderQueue={(fromIndex, toIndex) => setState({ type: 'reorder', from: fromIndex, to: toIndex })}
          playFromIndex={playFromIndex}
        />
        <PlayerVolume volume={volume} isMuted={isMuted} setVolume={setVolume} toggleMute={toggleMute} />
      </View>
    </View>
  );
}
