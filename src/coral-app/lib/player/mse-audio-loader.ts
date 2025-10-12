import { baseUrl } from '@/lib/client/fetcher';
import M3U8Parser from '@/lib/vendor/hls.js/src/loader/m3u8-parser';
import type { LevelDetails } from '@/lib/vendor/hls.js/src/loader/level-details';

interface TrackInfo {
  trackId: string;
  playlistUrl: string;
  mediaUrl: string;
  levelDetails: LevelDetails | null;
  initSegment: ArrayBuffer | null;
  currentFragmentIndex: number;
  isComplete: boolean;
  bufferStartTime: number;
  bufferEndTime: number;
}

export class MSEAudioLoader {
  private audioElement: HTMLAudioElement;
  private mediaSource: MediaSource | null = null;
  private sourceBuffer: SourceBuffer | null = null;
  private tracks: Map<string, TrackInfo> = new Map();
  private currentTrackId: string | null = null;
  private isAppending: boolean = false;
  private appendQueue: ArrayBuffer[] = [];
  private bufferAheadTarget = 30; // Keep 30 seconds buffered ahead
  private bufferBehindLimit = 60; // Remove data older than 60 seconds
  private nextBufferStartTime = 0; // Track where the next track should start in buffer timeline

  constructor() {
    this.audioElement = new Audio();
    this.setupAudioEventListeners();
  }

  private setupAudioEventListeners() {
    // Monitor playback to trigger progressive loading
    this.audioElement.addEventListener('timeupdate', () => {
      this.checkBufferAndLoad();
    });
  }

  async initialize(trackId: string): Promise<void> {
    console.info('[MSE] 🎬 Initializing MSE for track:', trackId);

    // Get playlist URL and parse it
    const playlistUrl = await this.getPlaylistUrl(trackId);
    const levelDetails = await this.fetchAndParsePlaylist(playlistUrl);
    const mediaUrl = levelDetails.fragments[0]?.url || '';

    console.info('[MSE] Playlist has', levelDetails.fragments.length, 'fragments');
    console.info('[MSE] Media URL:', mediaUrl);

    // Calculate track duration
    const trackDuration = levelDetails.fragments.reduce((total, fragment) => {
      return total + fragment.duration;
    }, 0);

    // Store track info
    this.tracks.set(trackId, {
      trackId,
      playlistUrl,
      mediaUrl,
      levelDetails,
      initSegment: null,
      currentFragmentIndex: 0,
      isComplete: !levelDetails.live,
      bufferStartTime: this.nextBufferStartTime,
      bufferEndTime: this.nextBufferStartTime + trackDuration,
    });

    this.currentTrackId = trackId;
    this.nextBufferStartTime += trackDuration;

    // Create MediaSource
    this.mediaSource = new MediaSource();
    this.audioElement.src = URL.createObjectURL(this.mediaSource);

    // Wait for MediaSource to open
    await new Promise<void>((resolve, reject) => {
      if (!this.mediaSource) {
        reject(new Error('MediaSource is null'));
        return;
      }

      this.mediaSource.addEventListener('sourceopen', () => {
        console.info('[MSE] ✅ MediaSource opened');
        resolve();
      }, { once: true });

      this.mediaSource.addEventListener('error', (e) => {
        console.error('[MSE] ❌ MediaSource error:', e);
        reject(new Error('MediaSource failed to open'));
      }, { once: true });
    });

    // Create SourceBuffer with FLAC codec (hardcoded for POC)
    const mimeType = 'audio/mp4; codecs="flac"';

    if (!MediaSource.isTypeSupported(mimeType)) {
      throw new Error(`Browser doesn't support ${mimeType}`);
    }

    this.sourceBuffer = this.mediaSource!.addSourceBuffer(mimeType);
    this.sourceBuffer.mode = 'sequence';

    console.info('[MSE] ✅ SourceBuffer created');

    // Load init segment
    await this.loadInitSegment(trackId);

    // Load first 3 fragments (~30 seconds)
    await this.loadFragments(trackId, 3);
  }

  async appendTrack(trackId: string): Promise<void> {
    console.info('[MSE] 📋 Queuing track for gapless:', trackId);

    const playlistUrl = await this.getPlaylistUrl(trackId);
    const levelDetails = await this.fetchAndParsePlaylist(playlistUrl);
    const mediaUrl = levelDetails.fragments[0]?.url || '';

    // Calculate track duration
    const trackDuration = levelDetails.fragments.reduce((total, fragment) => {
      return total + fragment.duration;
    }, 0);

    this.tracks.set(trackId, {
      trackId,
      playlistUrl,
      mediaUrl,
      levelDetails,
      initSegment: null,
      currentFragmentIndex: 0,
      isComplete: !levelDetails.live,
      bufferStartTime: this.nextBufferStartTime,
      bufferEndTime: this.nextBufferStartTime + trackDuration,
    });

    this.nextBufferStartTime += trackDuration;

    console.info('[MSE] ✅ Track queued (buffer time:', this.tracks.get(trackId)?.bufferStartTime, '-', this.tracks.get(trackId)?.bufferEndTime, ')');
  }

  private async loadInitSegment(trackId: string): Promise<void> {
    const trackInfo = this.tracks.get(trackId);
    if (!trackInfo || !trackInfo.levelDetails) return;

    const firstFragment = trackInfo.levelDetails.fragments[0];
    if (!firstFragment) return;

    const initSegmentEnd = firstFragment.byteRangeStartOffset!;
    console.info('[MSE] 📦 Fetching init segment (0-' + initSegmentEnd + ' bytes)');

    trackInfo.initSegment = await this.fetchByteRange(trackInfo.mediaUrl, 0, initSegmentEnd);
    await this.appendToSourceBuffer(trackInfo.initSegment);

    console.info('[MSE] ✓ Init segment appended');
  }

  private async loadFragments(trackId: string, count: number): Promise<void> {
    const trackInfo = this.tracks.get(trackId);
    if (!trackInfo || !trackInfo.levelDetails) return;

    const fragments = trackInfo.levelDetails.fragments;
    const startIndex = trackInfo.currentFragmentIndex;
    const endIndex = Math.min(startIndex + count, fragments.length);

    if (startIndex >= fragments.length) {
      console.info('[MSE] ✅ All fragments loaded for track:', trackId);
      return;
    }

    console.info('[MSE] 📥 Loading fragments', startIndex + 1, '-', endIndex, '/', fragments.length);

    for (let i = startIndex; i < endIndex; i++) {
      const fragment = fragments[i];
      const start = fragment.byteRangeStartOffset!;
      const end = fragment.byteRangeEndOffset!;
      const size = end - start;

      const fragmentData = await this.fetchByteRange(trackInfo.mediaUrl, start, size);

      // Concatenate init + fragment (MSE needs complete fMP4 boxes)
      const combined = this.concatenateBuffers(trackInfo.initSegment!, fragmentData);
      await this.appendToSourceBuffer(combined);

      trackInfo.currentFragmentIndex = i + 1;

      if (i === startIndex) {
        console.info('[MSE] ✓ First fragment appended');
      }
    }

    console.info('[MSE] ✅ Loaded', endIndex - startIndex, 'fragments');
  }

  private async checkBufferAndLoad(): Promise<void> {
    if (!this.sourceBuffer || !this.currentTrackId) return;

    const currentTime = this.audioElement.currentTime;
    const buffered = this.sourceBuffer.buffered;

    // Calculate buffered ahead
    let bufferedAhead = 0;
    for (let i = 0; i < buffered.length; i++) {
      const start = buffered.start(i);
      const end = buffered.end(i);
      if (currentTime >= start && currentTime <= end) {
        bufferedAhead = end - currentTime;
        break;
      }
    }

    // Load more if buffer is low
    if (bufferedAhead < this.bufferAheadTarget) {
      const trackInfo = this.tracks.get(this.currentTrackId);
      if (trackInfo && trackInfo.currentFragmentIndex < trackInfo.levelDetails!.fragments.length) {
        console.info('[MSE] 🔄 Buffer low (', bufferedAhead.toFixed(1), 's), loading more');
        await this.loadFragments(this.currentTrackId, 2);
      } else {
        // Current track is fully loaded, check if we should start loading next track
        const bufferInfo = this.getCurrentTrackBufferInfo();
        if (bufferInfo) {
          const timeUntilEnd = bufferInfo.bufferEndTime - currentTime;

          if (timeUntilEnd < this.bufferAheadTarget) {
            // Find next track in queue
            const nextTrackId = this.findNextQueuedTrack();
            if (nextTrackId) {
              const nextTrackInfo = this.tracks.get(nextTrackId);
              if (nextTrackInfo && nextTrackInfo.currentFragmentIndex === 0) {
                console.info('[MSE] 🎵 Near end of current track, loading next track:', nextTrackId);
                await this.loadInitSegment(nextTrackId);
                await this.loadFragments(nextTrackId, 3);
              }
            }
          }
        }
      }
    }

    // Remove old buffer
    this.removeOldBuffer(currentTime);
  }

  private findNextQueuedTrack(): string | null {
    // Find the next track that has been queued (not the current one)
    for (const [trackId, trackInfo] of this.tracks) {
      if (trackId !== this.currentTrackId && trackInfo.currentFragmentIndex === 0) {
        return trackId;
      }
    }
    return null;
  }

  private removeOldBuffer(currentTime: number): void {
    if (!this.sourceBuffer || this.sourceBuffer.updating) return;

    const buffered = this.sourceBuffer.buffered;
    for (let i = 0; i < buffered.length; i++) {
      const start = buffered.start(i);
      const end = buffered.end(i);

      if (end < currentTime - this.bufferBehindLimit) {
        try {
          console.info('[MSE] 🗑️  Removing old buffer:', start.toFixed(1), '-', end.toFixed(1));
          this.sourceBuffer.remove(start, end);
        } catch (e) {
          // Ignore errors
        }
        break;
      }
    }
  }

  private async appendToSourceBuffer(data: ArrayBuffer): Promise<void> {
    this.appendQueue.push(data);

    if (!this.isAppending) {
      await this.processAppendQueue();
    }
  }

  private async processAppendQueue(): Promise<void> {
    if (this.isAppending || this.appendQueue.length === 0) return;

    this.isAppending = true;

    while (this.appendQueue.length > 0) {
      const data = this.appendQueue.shift()!;

      if (this.sourceBuffer?.updating) {
        await this.waitForUpdateEnd();
      }

      if (!this.sourceBuffer || this.mediaSource?.readyState !== 'open') {
        console.warn('[MSE] ⚠️  SourceBuffer not available, dropping data');
        continue;
      }

      try {
        this.sourceBuffer.appendBuffer(data);
        await this.waitForUpdateEnd();
      } catch (error) {
        console.error('[MSE] ❌ Error appending buffer:', error);
      }
    }

    this.isAppending = false;
  }

  private waitForUpdateEnd(): Promise<void> {
    return new Promise((resolve) => {
      if (!this.sourceBuffer?.updating) {
        resolve();
        return;
      }

      this.sourceBuffer.addEventListener('updateend', () => {
        resolve();
      }, { once: true });
    });
  }

  private async getPlaylistUrl(trackId: string): Promise<string> {
    const response = await fetch(`${baseUrl}/api/library/tracks/${trackId}/stream`);

    if (!response.ok) {
      throw new Error(`Failed to get stream URL: ${response.status}`);
    }

    const data = await response.json();
    return data.link as string;
  }

  private async fetchAndParsePlaylist(url: string): Promise<LevelDetails> {
    const response = await fetch(url);

    if (!response.ok) {
      throw new Error(`Failed to fetch playlist: ${response.status}`);
    }

    const playlistText = await response.text();

    return M3U8Parser.parseLevelPlaylist(
      playlistText,
      url,
      0,
      'EVENT',
      0,
      null
    );
  }

  private async fetchByteRange(url: string, start: number, length: number): Promise<ArrayBuffer> {
    const end = start + length - 1;
    const response = await fetch(url, {
      headers: {
        'Range': `bytes=${start}-${end}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch byte range: ${response.status}`);
    }

    return await response.arrayBuffer();
  }

  private concatenateBuffers(buffer1: ArrayBuffer, buffer2: ArrayBuffer): ArrayBuffer {
    const combined = new Uint8Array(buffer1.byteLength + buffer2.byteLength);
    combined.set(new Uint8Array(buffer1), 0);
    combined.set(new Uint8Array(buffer2), buffer1.byteLength);
    return combined.buffer;
  }

  getAudioElement(): HTMLAudioElement {
    return this.audioElement;
  }

  /**
   * Get track duration from HLS playlist (sum of all fragment durations)
   */
  getTrackDuration(trackId: string): number {
    const trackInfo = this.tracks.get(trackId);
    if (!trackInfo || !trackInfo.levelDetails) return 0;

    // Sum all fragment durations from playlist
    return trackInfo.levelDetails.fragments.reduce((total, fragment) => {
      return total + fragment.duration;
    }, 0);
  }

  getCurrentTrackId(): string | null {
    return this.currentTrackId;
  }

  setCurrentTrackId(trackId: string): void {
    if (!this.tracks.has(trackId)) {
      console.warn('[MSE] ⚠️  Cannot set current track to', trackId, '- not in queue');
      return;
    }
    console.info('[MSE] 🔄 Switching current track to:', trackId);
    this.currentTrackId = trackId;
  }

  /**
   * Get the track ID that should be playing at the given buffer time
   */
  getTrackIdAtTime(time: number): string | null {
    for (const [trackId, trackInfo] of this.tracks) {
      if (time >= trackInfo.bufferStartTime && time < trackInfo.bufferEndTime) {
        return trackId;
      }
    }
    return null;
  }

  /**
   * Get the current track's buffer info
   */
  getCurrentTrackBufferInfo(): { bufferStartTime: number; bufferEndTime: number } | null {
    if (!this.currentTrackId) return null;
    const trackInfo = this.tracks.get(this.currentTrackId);
    if (!trackInfo) return null;
    return {
      bufferStartTime: trackInfo.bufferStartTime,
      bufferEndTime: trackInfo.bufferEndTime,
    };
  }

  destroy(): void {
    this.appendQueue = [];

    if (this.mediaSource?.readyState === 'open') {
      try {
        this.mediaSource.endOfStream();
      } catch (e) {
        // Ignore
      }
    }

    if (this.audioElement.src) {
      URL.revokeObjectURL(this.audioElement.src);
      this.audioElement.src = '';
    }

    this.sourceBuffer = null;
    this.mediaSource = null;
  }
}
