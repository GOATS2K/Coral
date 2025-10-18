import type { LevelDetails } from '@/lib/vendor/hls.js/src/loader/level-details';
import { FragmentLoader } from './fragment-loader';

interface TrackInfo {
  trackId: string;
  mediaUrl: string;
  levelDetails: LevelDetails | null;
  initSegment: ArrayBuffer | null;
  currentFragmentIndex: number;
  bufferStartTime: number;
  bufferEndTime: number;
  codec: string;
  isLoadingFragments: boolean;
}

export class MSEAudioLoader {
  private audioElement: HTMLAudioElement;
  private mediaSource: MediaSource | null = null;
  private sourceBuffer: SourceBuffer | null = null;
  private tracks: Map<string, TrackInfo> = new Map();
  private currentTrackId: string | null = null;
  private isAppending: boolean = false;
  private appendQueue: ArrayBuffer[] = [];
  private bufferAheadTarget = 20; // Keep 20 seconds buffered ahead
  private bufferBehindLimit = 30; // Remove data older than 30 seconds
  private nextBufferStartTime = 0; // Track where the next track should start in buffer timeline
  private fragmentLoader: FragmentLoader;

  constructor() {
    this.audioElement = new Audio();
    this.fragmentLoader = new FragmentLoader();
    this.setupAudioEventListeners();
  }

  private setupAudioEventListeners() {
    // Monitor playback to trigger progressive loading
    this.audioElement.addEventListener('timeupdate', () => {
      this.checkBufferAndLoad();
    });

    // Also check buffer after seeking completes
    this.audioElement.addEventListener('seeked', () => {
      this.checkBufferAndLoad();
    });
  }

  async initialize(trackId: string): Promise<void> {
    const { codec, levelDetails, mediaUrl, duration: trackDuration } = await this.fragmentLoader.fetchStreamInfo(trackId);

    // Get MIME type for codec
    const mimeType = this.getCodecMimeType(codec);

    // Validate browser support
    if (!MediaSource.isTypeSupported(mimeType)) {
      throw new Error(`Browser doesn't support ${mimeType}`);
    }

    // Store track info
    this.tracks.set(trackId, {
      trackId,
      mediaUrl,
      levelDetails,
      initSegment: null,
      currentFragmentIndex: 0,
      bufferStartTime: this.nextBufferStartTime,
      bufferEndTime: this.nextBufferStartTime + trackDuration,
      codec,
      isLoadingFragments: false,
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
        resolve();
      }, { once: true });

      this.mediaSource.addEventListener('error', (e) => {
        console.error('[MSE] ❌ MediaSource error:', e);
        reject(new Error('MediaSource failed to open'));
      }, { once: true });
    });

    // Create SourceBuffer with dynamic codec
    this.sourceBuffer = this.mediaSource!.addSourceBuffer(mimeType);

    // Use 'segments' mode to preserve timestamps from fMP4 container
    this.sourceBuffer.mode = 'segments';

    // Load init segment
    await this.loadInitSegment(trackId);

    // Load first 2 fragments (~20 seconds) - conservative due to 50MB buffer quota
    await this.loadFragments(trackId, 2);
  }

  async appendTrack(trackId: string): Promise<void> {
    const { codec, levelDetails, mediaUrl, duration: trackDuration } = await this.fragmentLoader.fetchStreamInfo(trackId);

    this.tracks.set(trackId, {
      trackId,
      mediaUrl,
      levelDetails,
      initSegment: null,
      currentFragmentIndex: 0,
      bufferStartTime: this.nextBufferStartTime,
      bufferEndTime: this.nextBufferStartTime + trackDuration,
      codec,
      isLoadingFragments: false,
    });

    this.nextBufferStartTime += trackDuration;
  }

  private async loadInitSegment(trackId: string): Promise<void> {
    const trackInfo = this.tracks.get(trackId);
    if (!trackInfo || !trackInfo.levelDetails) return;

    const firstFragment = trackInfo.levelDetails.fragments[0];
    if (!firstFragment) return;

    const initSegmentEnd = firstFragment.byteRangeStartOffset;
    if (!initSegmentEnd) {
      throw new Error('Init segment byte range not found');
    }

    trackInfo.initSegment = await this.fragmentLoader.fetchInitSegment(
      trackInfo.mediaUrl,
      initSegmentEnd,
      trackInfo.codec
    );

    // Set timestamp offset for gapless playback
    // This offsets the entire track's timestamps to the correct position in the buffer timeline
    if (this.sourceBuffer) {
      this.sourceBuffer.timestampOffset = trackInfo.bufferStartTime;
    }

    // Init segment is stored but will be concatenated with the first fragment
  }

  private async loadFragments(trackId: string, count: number): Promise<void> {
    const trackInfo = this.tracks.get(trackId);
    if (!trackInfo || !trackInfo.levelDetails) return;
    if (trackInfo.isLoadingFragments) return;

    const startIndex = trackInfo.currentFragmentIndex;

    // For live/EVENT streams, ALWAYS refetch playlist before loading fragments
    // to ensure we have the latest fragment data
    if (trackInfo.levelDetails.live) {
      try {
        const updatedInfo = await this.fragmentLoader.fetchStreamInfo(trackId);
        trackInfo.levelDetails = updatedInfo.levelDetails;
      } catch (error) {
        console.error('[MSE] Failed to refetch playlist:', error);
        return;
      }
    }

    const fragments = trackInfo.levelDetails.fragments;

    // If still out of bounds after refetch, fragment doesn't exist yet
    if (startIndex >= fragments.length) {
      return;
    }

    const endIndex = Math.min(startIndex + count, fragments.length);

    trackInfo.isLoadingFragments = true;
    const originalIndex = trackInfo.currentFragmentIndex;
    trackInfo.currentFragmentIndex = endIndex;

    try {
      await this.loadFragmentRange(trackInfo, fragments, startIndex, endIndex);
    } catch (error) {
      console.error('[MSE] Error loading fragments:', error);
      trackInfo.currentFragmentIndex = originalIndex;
    } finally {
      trackInfo.isLoadingFragments = false;
    }
  }

  private async loadFragmentRange(
    trackInfo: TrackInfo,
    fragments: any[],
    startIndex: number,
    endIndex: number
  ): Promise<void> {
    for (let i = startIndex; i < endIndex; i++) {
      const fragment = fragments[i];
      const start = fragment.byteRangeStartOffset;
      const end = fragment.byteRangeEndOffset;

      const fragmentData = await this.fragmentLoader.fetchFragment(
        trackInfo.mediaUrl,
        start,
        end,
        trackInfo.codec
      );

      // For the very first fragment of this track, concatenate init segment
      if (i === 0 && trackInfo.initSegment) {
        const combined = this.concatenateBuffers(trackInfo.initSegment, fragmentData);
        await this.appendToSourceBuffer(combined);
      } else {
        // For subsequent fragments, append directly
        await this.appendToSourceBuffer(fragmentData);
      }
    }
  }

  private concatenateBuffers(buffer1: ArrayBuffer, buffer2: ArrayBuffer): ArrayBuffer {
    const combined = new Uint8Array(buffer1.byteLength + buffer2.byteLength);
    combined.set(new Uint8Array(buffer1), 0);
    combined.set(new Uint8Array(buffer2), buffer1.byteLength);
    return combined.buffer;
  }

  async checkBufferAndLoad(): Promise<void> {
    if (!this.sourceBuffer || !this.currentTrackId) return;

    const currentTime = this.audioElement.currentTime;

    // Remove old buffer first to free up quota
    this.removeOldBuffer(currentTime);

    // Clean up buffer ranges that are far from current position
    await this.cleanUpDistantBuffers(currentTime);

    const buffered = this.sourceBuffer.buffered;

    // Calculate buffered ahead
    let bufferedAhead = 0;
    let inBufferedRange = false;
    for (let i = 0; i < buffered.length; i++) {
      const start = buffered.start(i);
      const end = buffered.end(i);
      if (currentTime >= start && currentTime <= end) {
        bufferedAhead = end - currentTime;
        inBufferedRange = true;
        break;
      }
    }

    // Only load if buffer is getting low
    const needsData = !inBufferedRange || bufferedAhead < this.bufferAheadTarget;

    if (needsData) {
      const trackInfo = this.tracks.get(this.currentTrackId);

      if (trackInfo && trackInfo.currentFragmentIndex < trackInfo.levelDetails!.fragments.length) {
        // Only correct timestampOffset if it's pointing to a different track's timeline
        // (don't correct during normal progressive loading or when next track is pre-loading)
        if (this.sourceBuffer) {
          const currentTrackEnd = trackInfo.bufferStartTime + this.getTrackDuration(this.currentTrackId);
          const offsetOutsideCurrentTrack = this.sourceBuffer.timestampOffset < trackInfo.bufferStartTime ||
                                             this.sourceBuffer.timestampOffset >= currentTrackEnd;

          if (offsetOutsideCurrentTrack) {
            this.sourceBuffer.timestampOffset = trackInfo.bufferStartTime;
          }
        }
        // Load 1 fragment at a time to avoid quota issues
        await this.loadFragments(this.currentTrackId, 1);
      } else if (trackInfo && trackInfo.levelDetails && trackInfo.levelDetails.live) {
        // Live stream - all current fragments loaded, refetch playlist for new fragments
        try {
          const updatedInfo = await this.fragmentLoader.fetchStreamInfo(this.currentTrackId);
          trackInfo.levelDetails = updatedInfo.levelDetails;

          // Check if new fragments are available
          if (trackInfo.currentFragmentIndex < trackInfo.levelDetails.fragments.length) {
            await this.loadFragments(this.currentTrackId, 1);
          }
        } catch (error) {
          console.error('[MSE] Failed to refetch live playlist:', error);
        }
      } else {
        // Current track is fully loaded (non-live), check if we should start loading next track
        const bufferInfo = this.getCurrentTrackBufferInfo();
        if (bufferInfo) {
          const timeUntilEnd = bufferInfo.bufferEndTime - currentTime;

          if (timeUntilEnd < this.bufferAheadTarget) {
            // Find next track in queue
            const nextTrackId = this.findNextQueuedTrack();
            if (nextTrackId) {
              const nextTrackInfo = this.tracks.get(nextTrackId);
              if (nextTrackInfo && nextTrackInfo.currentFragmentIndex === 0) {
                await this.loadInitSegment(nextTrackId);
                // Load 2 fragments for next track to ensure smooth transition
                await this.loadFragments(nextTrackId, 2);
              }
            }
          }
        }
      }
    }
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
          this.sourceBuffer.remove(start, end);
        } catch {
          // Ignore errors
        }
        break;
      }
    }
  }

  /**
   * Clean up buffer ranges that are far from the current playback position
   * This helps prevent buffer quota issues when seeking
   */
  private async cleanUpDistantBuffers(currentTime: number): Promise<void> {
    if (!this.sourceBuffer || this.sourceBuffer.updating) return;

    const buffered = this.sourceBuffer.buffered;
    const maxBufferRange = 40; // Keep max 40 seconds around current position (tight limit due to ~50MB quota)

    for (let i = 0; i < buffered.length; i++) {
      const start = buffered.start(i);
      const end = buffered.end(i);

      // Remove ranges that are completely outside our buffer window
      if (end < currentTime - this.bufferBehindLimit || start > currentTime + maxBufferRange) {
        try {
          this.sourceBuffer.remove(start, end);
          // Wait for remove operation to complete before continuing
          await this.waitForUpdateEnd();
          // Can only remove one range at a time, exit after first removal
          return;
        } catch (error) {
          console.error('[MSE] Error during buffer removal:', error);
        }
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

  private findFragmentIndexForTime(fragments: any[], relativeTime: number): number {
    if (fragments.length === 0) return 0;

    let cumulativeTime = 0;

    for (let i = 0; i < fragments.length; i++) {
      cumulativeTime += fragments[i].duration;
      if (cumulativeTime > relativeTime) {
        return i;
      }
    }

    // If we've gone through all fragments and haven't reached the target time,
    // calculate the fragment index based on average fragment duration
    const avgFragmentDuration = cumulativeTime / fragments.length;
    const additionalTime = relativeTime - cumulativeTime;
    const additionalFragments = Math.ceil(additionalTime / avgFragmentDuration);

    return fragments.length + additionalFragments - 1;
  }

  // Reset fragment index to match a seek position
  resetToPosition(absoluteTime: number): void {
    if (!this.currentTrackId) return;

    const trackInfo = this.tracks.get(this.currentTrackId);
    if (!trackInfo || !trackInfo.levelDetails) return;

    // Calculate relative time within the current track
    const relativeTime = absoluteTime - trackInfo.bufferStartTime;

    trackInfo.currentFragmentIndex = this.findFragmentIndexForTime(
      trackInfo.levelDetails.fragments,
      relativeTime
    );
  }

  /**
   * Check if playlist needs refetching and refetch if necessary
   * Returns true if playlist was refetched
   */
  async checkAndRefetchIfNeeded(): Promise<boolean> {
    if (!this.currentTrackId) return false;

    const trackInfo = this.tracks.get(this.currentTrackId);
    if (!trackInfo?.levelDetails) return false;

    // If fragment index is beyond available and stream is live, refetch playlist
    if (trackInfo.currentFragmentIndex >= trackInfo.levelDetails.fragments.length &&
        trackInfo.levelDetails.live) {
      try {
        const updatedInfo = await this.fragmentLoader.fetchStreamInfo(this.currentTrackId);
        trackInfo.levelDetails = updatedInfo.levelDetails;
        return true;
      } catch (error) {
        console.error('[MSE] Failed to refetch playlist before seek:', error);
        return false;
      }
    }
    return false;
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
      console.warn('[MSE] Cannot set current track - not in queue:', trackId);
      return;
    }
    this.currentTrackId = trackId;
  }

  getMediaSourceState(): string {
    return this.mediaSource?.readyState || 'null';
  }

  getSourceBufferState(): { updating: boolean; bufferedRanges: string[] } | null {
    if (!this.sourceBuffer) return null;
    const buffered = this.sourceBuffer.buffered;
    return {
      updating: this.sourceBuffer.updating,
      bufferedRanges: Array.from({length: buffered.length}, (_, i) =>
        `${buffered.start(i).toFixed(1)}-${buffered.end(i).toFixed(1)}`)
    };
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

  private getCodecMimeType(codec: string): string {
    const normalizedCodec = codec.toLowerCase();

    const codecMap: Record<string, string> = {
      'flac': 'audio/mp4; codecs="flac"',
      'aac': 'audio/mp4; codecs="mp4a.40.2"',
      'alac': 'audio/mp4; codecs="alac"',
    };

    if (codecMap[normalizedCodec]) {
      return codecMap[normalizedCodec];
    }

    throw new Error(`Unsupported codec for MSE: ${codec}`);
  }

  destroy(): void {
    this.appendQueue = [];

    if (this.mediaSource?.readyState === 'open') {
      try {
        this.mediaSource.endOfStream();
      } catch {
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
