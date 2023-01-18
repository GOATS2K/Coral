import {create} from "zustand";
import { TrackDto } from "./client";

export interface PlayerState {
  playState: boolean;
  selectedTrack: TrackDto;
}

export const usePlayerStore = create<PlayerState>()((set) => ({
  selectedTrack: {} as TrackDto,
  playState: false,
}));
