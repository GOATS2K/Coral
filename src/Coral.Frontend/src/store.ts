import create from "zustand";
import { StreamDto, TrackDto } from "./client";

export interface PlayerState {
  playState: boolean;
  selectedTrack: TrackDto;
}

export const usePlayerStore = create<PlayerState>()((set) => ({
  selectedTrack: {} as TrackDto,
  playState: false,
}));
