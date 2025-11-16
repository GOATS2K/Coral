import React, { useEffect } from 'react';
import { useSetAtom } from 'jotai';
import { HubConnectionState } from '@microsoft/signalr';
import { signalRService } from './signalr-service';
import {
  signalrConnectionStateAtom,
  updateScanProgressAtom,
  syncActivescansAtom,
  removeScanProgressAtom,
  addSignalREventAtom,
} from './signalr-atoms';

interface SignalRProviderProps {
  children: React.ReactNode;
}

export function SignalRProvider({ children }: SignalRProviderProps) {
  const setConnectionState = useSetAtom(signalrConnectionStateAtom);
  const updateScanProgress = useSetAtom(updateScanProgressAtom);
  const syncActiveScans = useSetAtom(syncActivescansAtom);
  const removeScanProgress = useSetAtom(removeScanProgressAtom);
  const addSignalREvent = useSetAtom(addSignalREventAtom);

  useEffect(() => {
    let mounted = true;

    const connectSignalR = async () => {
      try {
        // Set up event handlers
        signalRService.on('connectionStateChanged', (state: string) => {
          if (!mounted) return;

          const stateMap: Record<string, HubConnectionState> = {
            'connected': HubConnectionState.Connected,
            'reconnecting': HubConnectionState.Reconnecting,
            'disconnected': HubConnectionState.Disconnected,
          };

          setConnectionState(stateMap[state] ?? HubConnectionState.Disconnected);
        });

        signalRService.on('scanProgress', (progress) => {
          if (!mounted) return;

          console.info('[SignalRProvider] Scan progress update:', progress);
          updateScanProgress(progress);

          // Also add as generic event for logging
          addSignalREvent({
            type: 'scanProgress',
            data: progress,
          });
        });

        signalRService.on('scanComplete', (complete) => {
          if (!mounted) return;

          console.info('[SignalRProvider] Scan complete:', complete);

          // Show completion summary for 5 seconds before removing
          const completionSummary = {
            requestId: complete.requestId,
            libraryName: complete.libraryName,
            tracksAdded: complete.tracksAdded,
            tracksUpdated: complete.tracksUpdated,
            tracksDeleted: complete.tracksDeleted,
            embeddingsCompleted: complete.embeddingsCompleted,
            isComplete: true,
          };

          updateScanProgress(completionSummary);

          // Remove after 5 seconds
          setTimeout(() => {
            if (mounted) {
              removeScanProgress(complete.requestId);
            }
          }, 5000);

          // Add as generic event for logging
          addSignalREvent({
            type: 'scanComplete',
            data: complete,
          });
        });

        signalRService.on('syncActiveScans', (scans) => {
          if (!mounted) return;

          console.info('[SignalRProvider] Syncing active scans:', scans);
          syncActiveScans(scans);
        });

        // Connect to SignalR hub
        await signalRService.connect();
        setConnectionState(HubConnectionState.Connected);
      } catch (error) {
        console.error('[SignalRProvider] Failed to connect:', error);
        setConnectionState(HubConnectionState.Disconnected);
      }
    };

    // Delay connection slightly to ensure API config is loaded
    const timer = setTimeout(() => {
      connectSignalR();
    }, 500);

    return () => {
      mounted = false;
      clearTimeout(timer);

      // Disconnect when unmounting
      signalRService.disconnect().catch(console.error);
    };
  }, [setConnectionState, updateScanProgress, removeScanProgress, syncActiveScans, addSignalREvent]);

  return <>{children}</>;
}