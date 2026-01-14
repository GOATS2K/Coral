import * as signalR from '@microsoft/signalr';
import { Config } from '../config';

export interface ScanProgressData {
  requestId: string;
  libraryName: string;
  expectedTracks: number;
  tracksAdded: number;
  tracksUpdated: number;
  tracksDeleted: number;
  embeddingsCompleted: number;
  isComplete?: boolean; // Set to true when showing completion summary
  isFailed?: boolean; // Set to true when scan failed
  errorMessage?: string; // Error message if failed
}

export interface ScanJobProgress {
  requestId: string;
  libraryId: string;
  libraryName: string;
  expectedTracks: number;
  tracksAdded: number;
  tracksUpdated: number;
  tracksDeleted: number;
  embeddingsCompleted: number;
  startedAt: string;
}

export interface ScanCompleteData {
  requestId: string;
  libraryName: string;
  tracksAdded: number;
  tracksUpdated: number;
  tracksDeleted: number;
  embeddingsCompleted: number;
  duration: string; // TimeSpan format: "HH:MM:SS.mmmmmmm"
}

export interface ScanFailedData {
  requestId: string;
  libraryName: string;
  errorMessage: string;
  duration: string; // TimeSpan format: "HH:MM:SS.mmmmmmm"
}

export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private eventHandlers: Map<string, Set<(data: any) => void>> = new Map();

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    const baseUrl = await Config.getBackendUrl();
    const hubUrl = `${baseUrl}/hubs/library`;

    console.info('[SignalR] Connecting to hub:', hubUrl);

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Set up event handlers before connecting
    this.setupConnectionEvents();

    // Register hub method handlers
    this.registerHubHandlers();

    try {
      await this.connection.start();
      console.info('[SignalR] Connected successfully');

      // Get active scans on connection
      await this.getActiveScans();
    } catch (error) {
      console.error('[SignalR] Connection failed:', error);
      throw error;
    }
  }

  private setupConnectionEvents(): void {
    if (!this.connection) return;

    this.connection.onreconnecting(() => {
      console.info('[SignalR] Reconnecting...');
      this.emit('connectionStateChanged', 'reconnecting');
    });

    this.connection.onreconnected(() => {
      console.info('[SignalR] Reconnected');
      this.emit('connectionStateChanged', 'connected');
      // Re-fetch active scans after reconnection
      this.getActiveScans();
    });

    this.connection.onclose(() => {
      console.info('[SignalR] Connection closed');
      this.emit('connectionStateChanged', 'disconnected');
    });
  }

  private registerHubHandlers(): void {
    if (!this.connection) return;

    // Library scan progress handler
    this.connection.on('LibraryScanProgress', (progress: ScanProgressData) => {
      console.info('[SignalR] Scan progress:', progress);
      this.emit('scanProgress', progress);
    });

    // Library scan complete handler
    this.connection.on('LibraryScanComplete', (complete: ScanCompleteData) => {
      console.info('[SignalR] Scan complete:', complete);
      this.emit('scanComplete', complete);
    });

    // Library scan failed handler
    this.connection.on('LibraryScanFailed', (failed: ScanFailedData) => {
      console.info('[SignalR] Scan failed:', failed);
      this.emit('scanFailed', failed);
    });

    // Add more handlers here as needed for future features
  }

  async getActiveScans(): Promise<ScanJobProgress[]> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      console.warn('[SignalR] Cannot get active scans - not connected');
      return [];
    }

    try {
      const activeScans = await this.connection.invoke<ScanJobProgress[]>('GetActiveScans');
      console.info('[SignalR] Active scans:', activeScans);

      // Emit a sync event with all active scans (replaces current state)
      const scanDataArray = activeScans.map(scan => ({
        requestId: scan.requestId,
        libraryName: scan.libraryName,
        expectedTracks: scan.expectedTracks,
        tracksAdded: scan.tracksAdded,
        tracksUpdated: scan.tracksUpdated,
        tracksDeleted: scan.tracksDeleted,
        embeddingsCompleted: scan.embeddingsCompleted,
      }));

      this.emit('syncActiveScans', scanDataArray);

      return activeScans;
    } catch (error) {
      console.error('[SignalR] Failed to get active scans:', error);
      return [];
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.eventHandlers.clear();
    }
  }

  // Event emitter pattern for decoupling
  on(event: string, handler: (data: any) => void): void {
    if (!this.eventHandlers.has(event)) {
      this.eventHandlers.set(event, new Set());
    }
    this.eventHandlers.get(event)!.add(handler);
  }

  off(event: string, handler: (data: any) => void): void {
    this.eventHandlers.get(event)?.delete(handler);
  }

  private emit(event: string, data: any): void {
    this.eventHandlers.get(event)?.forEach(handler => handler(data));
  }

  get connectionState(): signalR.HubConnectionState | null {
    return this.connection?.state ?? null;
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

// Singleton instance
export const signalRService = new SignalRService();