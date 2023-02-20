import { create } from "zustand";
import { StreamDto, TrackDto } from "./client/schemas";

export enum PlayerInitializationSource {
  Album = "/albums",
  Search = "/search"
}

export interface Initializer {
  source: PlayerInitializationSource;
  id: number;
}

export interface PlayerState {
  playState: boolean;
  tracks: TrackDto[];
  // currentStream: StreamDto;
  initializer: Initializer;
  selectedTrack: TrackDto;
  getIndexOfSelectedTrack: () => number;
  nextTrack: () => void;
  prevTrack: () => void;
}

export const usePlayerStore = create<PlayerState>()((set, get) => ({
  playState: false,
  selectedTrack: {} as TrackDto,
  tracks: [] as TrackDto[],
  // currentStream: {} as StreamDto,
  initializer: {} as Initializer,
  getIndexOfSelectedTrack: () => {
    return get().tracks.indexOf(get().selectedTrack);
  },
  nextTrack: () => {
    const tracks = get().tracks;
    const selectedTrack = get().selectedTrack;
    if (tracks.indexOf(selectedTrack) !== tracks.length - 1) {
      set(() => ({
        selectedTrack: tracks[get().getIndexOfSelectedTrack() + 1],
      }));
    } else {
      // stop playing when we've reached the end
      set(() => ({ playState: false }));
    }
  },
  prevTrack: () => {
    const tracks = get().tracks;
    const selectedTrack = get().selectedTrack;
    if (tracks.indexOf(selectedTrack) !== 0) {
      set(() => ({
        selectedTrack: tracks[get().getIndexOfSelectedTrack() - 1],
      }));
    }
  },
}));
