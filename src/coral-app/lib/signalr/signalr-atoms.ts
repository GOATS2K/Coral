import { atom } from 'jotai';
import { HubConnectionState } from '@microsoft/signalr';
import type { ScanProgressData } from './signalr-service';

// Connection state atom
export const signalrConnectionStateAtom = atom<HubConnectionState | null>(null);

// Active scan progress - keyed by requestId
export const scanProgressMapAtom = atom<Map<string, ScanProgressData>>(new Map());

// Computed atom for active scans as array
export const activeScanProgressAtom = atom((get) => {
  const progressMap = get(scanProgressMapAtom);
  return Array.from(progressMap.values());
});

// Generic events atom for future extensibility
export interface SignalREvent {
  type: string;
  data: any;
  timestamp: Date;
}

export const signalrEventsAtom = atom<SignalREvent[]>([]);

// Add event helper
export const addSignalREventAtom = atom(
  null,
  (get, set, event: Omit<SignalREvent, 'timestamp'>) => {
    const events = get(signalrEventsAtom);
    set(signalrEventsAtom, [
      ...events.slice(-99), // Keep last 100 events
      { ...event, timestamp: new Date() }
    ]);
  }
);

// Update scan progress helper
export const updateScanProgressAtom = atom(
  null,
  (get, set, progress: ScanProgressData) => {
    const progressMap = new Map(get(scanProgressMapAtom));

    // Store the progress
    progressMap.set(progress.requestId, progress);
    set(scanProgressMapAtom, progressMap);

    // Note: The backend removes completed scans automatically,
    // so they won't appear in GetActiveScans on reconnection
  }
);

// Sync active scans from server (replaces current map)
export const syncActivescansAtom = atom(
  null,
  (get, set, activeScans: ScanProgressData[]) => {
    const newMap = new Map<string, ScanProgressData>();
    activeScans.forEach(scan => {
      newMap.set(scan.requestId, scan);
    });
    set(scanProgressMapAtom, newMap);
  }
);

// Clear all scan progress
export const clearScanProgressAtom = atom(
  null,
  (get, set) => {
    set(scanProgressMapAtom, new Map());
  }
);