import getPrefferedColor from '@/components/util/getPreferredColor'
import { atom } from 'jotai'

export type Colors = 'light' | 'dark'

export const themeAtom = atom<Colors>('dark')