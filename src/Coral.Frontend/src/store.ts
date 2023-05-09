import { create } from "zustand";
import { SearchResult, TrackDto } from "./client/schemas";
import { getTrackArtists } from "./common/album";

export enum PlayerInitializationSource {
  Album = "/albums",
  Search = "/search",
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
  getMainArtists: () => string;
  getIndexOfSelectedTrack: () => number;
  nextTrack: () => void;
  prevTrack: () => void;
}

export interface SearchState {
  query: string;
  result: SearchResult;
  currentPage: number;
  pages: number;
  setQueryString: (query: string) => void;
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
    return get().tracks.findIndex((t) => t.id === get().selectedTrack.id);
  },
  nextTrack: () => {
    const tracks = get().tracks;
    if (get().getIndexOfSelectedTrack() !== tracks.length - 1) {
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

export const useSearchStore = create<SearchState>()((set) => ({
  query: "",
  result: {} as SearchResult,
  currentPage: 1,
  pages: 1,
  setQueryString: (query: string) => {
    set(() => ({ query: query, currentPage: 1, pages: 1 }));
  },
}));
