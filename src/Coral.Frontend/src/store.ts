import create from "zustand";
import { StreamDto, TrackDto } from "./client";

interface PlayerState {
  playState: boolean;
  selectedTrack: TrackDto;
  togglePlayState: () => void;
  setPlayState: (value: boolean) => void;
  setSelectedTrack: (track: TrackDto) => void;
}

export const usePlayerStore = create<PlayerState>()((set) => ({
  selectedTrack: {} as TrackDto,
  playState: false,
  togglePlayState: () =>
    set((state: PlayerState) => ({
      playState: !state.playState,
    })),
  setPlayState: (value: boolean) =>
    set(() => ({
      playState: value,
    })),
  setSelectedTrack: (track: TrackDto) =>
    set((state) => ({
      selectedTrack: track,
    })),
}));
