import { create } from "zustand";
import { SearchResult, TrackDto } from "./client/schemas";
import { getTrackArtists } from "./common/album";

export enum PlayerInitializationSource {
  Album = "/albums",
  Search = "/search",
}

export interface Initializer {
  source: PlayerInitializationSource;
  id: string;
}

export interface PlayerState {
  playState: boolean;
  tracks: TrackDto[];
  // currentStream: StreamDto;
  initializer: Initializer;
  selectedTrack: TrackDto;
  getMainArtists: () => string;
  getIndexOfSelectedTrack: () => number;
  nextTrack: () => void;
  prevTrack: () => void;
}

export interface SearchState {
  query: string;
  result: SearchResult;
}

export const usePlayerStore = create<PlayerState>()((set, get) => ({
  playState: false,
  selectedTrack: {} as TrackDto,
  tracks: [] as TrackDto[],
  // currentStream: {} as StreamDto,
  initializer: {} as Initializer,
  getMainArtists: () => {
    if (get().selectedTrack.artists == null) {
      return "";
    }
    return getTrackArtists(get().selectedTrack);
  },
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

export const useSearchStore = create<SearchState>()(() => ({
  query: "",
  result: {} as SearchResult,
}));
