import { atom } from 'jotai'
import { SimpleTrackDto, AlbumDto } from './client/schemas'

export type Colors = 'light' | 'dark'

export const themeAtom = atom<Colors>('dark')

export interface PlayerState {
  currentTrack: SimpleTrackDto | null;
  currentAlbum: AlbumDto | null;
  queue: SimpleTrackDto[];
  currentIndex: number;
}

export const playerStateAtom = atom<PlayerState>({
  currentTrack: null,
  currentAlbum: null,
  queue: [],
  currentIndex: 0,
})