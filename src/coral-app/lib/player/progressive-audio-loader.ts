import M3U8Parser from '@/lib/vendor/hls.js/src/loader/m3u8-parser';
import { baseUrl } from '@/lib/client/fetcher';
import type { LevelDetails } from '@/lib/vendor/hls.js/src/loader/level-details';

interface ChunkCache {
  audioBuffer: AudioBuffer;
  fragmentIndex: number;
  timestamp: number;
}

interface TrackChunks {
  initSegment: ArrayBuffer | null;
  chunks: Map<number, ChunkCache>;
  playlistUrl: string;
  mediaUrl: string;
  lastPlaylistFetch: number;
  levelDetails: LevelDetails | null; // Latest playlist metadata
  isPlaylistComplete: boolean; // True when #EXT-X-ENDLIST seen (live: false)
}

export class ProgressiveAudioLoader {
  private audioContext: AudioContext;
  private trackCache: Map<string, TrackChunks> = new Map();
  private abortControllers: Map<string, AbortController> = new Map();

  // Cache config: keep last 30 chunks (5 minutes at 10s/chunk)
  private readonly MAX_CHUNKS_PER_TRACK = 30;

  constructor(audioContext: AudioContext) {
    this.audioContext = audioContext;
  }

  /**
   * Load a track's playlist and return chunks as they're decoded
   */
  async *loadTrack(trackId: string): AsyncGenerator<AudioBuffer, void, undefined> {
    console.info('[ProgressiveLoader] 🎵 Loading track:', trackId);

    // Get or create track cache entry
    let trackChunks = this.trackCache.get(trackId);

    if (!trackChunks) {
      // Get playlist URL from backend
      console.info('[ProgressiveLoader] Fetching stream URL from backend');
      const playlistUrl = await this.getPlaylistUrl(trackId);
      console.info('[ProgressiveLoader] Playlist URL:', playlistUrl);

      trackChunks = {
        initSegment: null,
        chunks: new Map(),
        playlistUrl,
        mediaUrl: '', // Will be set on first playlist fetch
        lastPlaylistFetch: 0,
        levelDetails: null, // Will be set on first playlist fetch
        isPlaylistComplete: false, // Will be set when #EXT-X-ENDLIST is seen
      };

      this.trackCache.set(trackId, trackChunks);
    }

    let chunkIndex = 0;
    let isComplete = false;

    // Stream chunks as they become available
    while (!isComplete) {
      let levelDetails: LevelDetails;

      // Only refetch if playlist is not complete or we need more fragments
      const needsRefetch = !trackChunks.isPlaylistComplete ||
                          !trackChunks.levelDetails ||
                          chunkIndex >= trackChunks.levelDetails.fragments.length;

      if (needsRefetch) {
        console.info('[ProgressiveLoader] 📋 Refetching playlist (looking for chunk', chunkIndex, ')');
        levelDetails = await this.fetchAndParsePlaylist(trackChunks.playlistUrl);
        trackChunks.levelDetails = levelDetails; // Cache latest playlist metadata

        // Mark playlist as complete if we see #EXT-X-ENDLIST
        if (!levelDetails.live && !trackChunks.isPlaylistComplete) {
          trackChunks.isPlaylistComplete = true;
          console.info('[ProgressiveLoader] ✅ Playlist complete (#EXT-X-ENDLIST found)');
        }

        console.info('[ProgressiveLoader] Playlist has', levelDetails.fragments.length, 'fragments, live:', levelDetails.live);
      } else {
        // Use cached playlist
        console.info('[ProgressiveLoader] 💾 Using cached playlist for chunk', chunkIndex);
        levelDetails = trackChunks.levelDetails!;
      }

      // Update media URL if not set
      if (!trackChunks.mediaUrl && levelDetails.fragments.length > 0) {
        trackChunks.mediaUrl = levelDetails.fragments[0].url;
        console.info('[ProgressiveLoader] Media URL:', trackChunks.mediaUrl);
      }

      // Fetch init segment if not cached
      if (!trackChunks.initSegment && levelDetails.fragments.length > 0) {
        const firstFragment = levelDetails.fragments[0];
        const initSegmentEnd = firstFragment.byteRangeStartOffset!;
        console.info('[ProgressiveLoader] 📦 Fetching init segment (0-' + initSegmentEnd + ')');
        trackChunks.initSegment = await this.fetchByteRange(
          trackChunks.mediaUrl,
          trackId,
          0,
          initSegmentEnd
        );
        console.info('[ProgressiveLoader] ✓ Init segment fetched, size:', (trackChunks.initSegment.byteLength / 1024).toFixed(2), 'KB');
      }

      // Check if requested chunk is available yet
      if (chunkIndex >= levelDetails.fragments.length) {
        // Check if playlist is complete
        if (levelDetails.live === false || levelDetails.fragments.length === 0) {
          console.info('[ProgressiveLoader] ✅ Playlist complete, all', chunkIndex, 'chunks loaded');
          isComplete = true;
          break;
        }

        // Wait a bit and refetch playlist
        console.info('[ProgressiveLoader] ⏳ Waiting for chunk', chunkIndex, 'to be available (playlist only has', levelDetails.fragments.length, 'so far)');
        await new Promise(resolve => setTimeout(resolve, 500));
        continue;
      }

      // Check cache first
      const cached = trackChunks.chunks.get(chunkIndex);
      if (cached) {
        console.info('[ProgressiveLoader] 💾 Cache hit for chunk', chunkIndex, '- serving from memory');
        cached.timestamp = Date.now(); // Update LRU
        yield cached.audioBuffer;
        chunkIndex++;
        continue;
      }

      // Fetch and decode chunk
      const fragment = levelDetails.fragments[chunkIndex];
      const startByte = fragment.byteRangeStartOffset!;
      const endByte = fragment.byteRangeEndOffset!;
      const chunkSize = endByte - startByte;
      console.info('[ProgressiveLoader] 📥 Fetching chunk', chunkIndex + 1, '/', levelDetails.fragments.length,
                   '- bytes', startByte, 'to', endByte, '(' + (chunkSize / 1024).toFixed(2), 'KB)');

      const fetchStart = performance.now();
      const chunkData = await this.fetchByteRange(
        trackChunks.mediaUrl,
        trackId,
        startByte,
        chunkSize
      );
      const fetchTime = performance.now() - fetchStart;
      console.info('[ProgressiveLoader] ✓ Chunk', chunkIndex, 'fetched in', fetchTime.toFixed(0), 'ms');

      // Concatenate init segment + chunk for decoding
      const fullSegment = this.concatenateBuffers(trackChunks.initSegment!, chunkData);

      console.info('[ProgressiveLoader] 🔊 Decoding chunk', chunkIndex, '(combined size:', (fullSegment.byteLength / 1024).toFixed(2), 'KB)');
      const decodeStart = performance.now();
      const audioBuffer = await this.audioContext.decodeAudioData(fullSegment);
      const decodeTime = performance.now() - decodeStart;
      console.info('[ProgressiveLoader] ✓ Chunk', chunkIndex, 'decoded in', decodeTime.toFixed(0), 'ms - duration:', audioBuffer.duration.toFixed(2), 's');

      // Cache the chunk
      trackChunks.chunks.set(chunkIndex, {
        audioBuffer,
        fragmentIndex: chunkIndex,
        timestamp: Date.now(),
      });
      console.info('[ProgressiveLoader] 💾 Cached chunk', chunkIndex, '(total cached:', trackChunks.chunks.size, '/', this.MAX_CHUNKS_PER_TRACK, ')');

      // Prune old chunks if exceeded max
      this.pruneChunkCache(trackChunks);

      yield audioBuffer;
      chunkIndex++;

      // Check if this was the last chunk
      if (levelDetails.live === false && chunkIndex >= levelDetails.fragments.length) {
        console.info('[ProgressiveLoader] ✅ All', chunkIndex, 'chunks loaded for track:', trackId);
        isComplete = true;
      }
    }
  }

  /**
   * Get a specific chunk by index (for seeking)
   */
  async getChunk(trackId: string, chunkIndex: number): Promise<AudioBuffer | null> {
    const trackChunks = this.trackCache.get(trackId);
    if (!trackChunks) {
      console.warn('[ProgressiveLoader] ⚠️  No playlist cached for track:', trackId);
      return null;
    }

    // Check cache
    const cached = trackChunks.chunks.get(chunkIndex);
    if (cached) {
      console.info('[ProgressiveLoader] 💾 Cache hit for on-demand chunk', chunkIndex);
      cached.timestamp = Date.now(); // Update LRU timestamp
      return cached.audioBuffer;
    }

    // Only refetch if playlist is not complete or we need more fragments
    let levelDetails: LevelDetails;
    const needsRefetch = !trackChunks.isPlaylistComplete ||
                        !trackChunks.levelDetails ||
                        chunkIndex >= trackChunks.levelDetails.fragments.length;

    if (needsRefetch) {
      console.info('[ProgressiveLoader] 📋 Refetching playlist for on-demand chunk', chunkIndex);
      levelDetails = await this.fetchAndParsePlaylist(trackChunks.playlistUrl);
      trackChunks.levelDetails = levelDetails; // Cache latest playlist metadata

      // Mark playlist as complete if we see #EXT-X-ENDLIST
      if (!levelDetails.live && !trackChunks.isPlaylistComplete) {
        trackChunks.isPlaylistComplete = true;
        console.info('[ProgressiveLoader] ✅ Playlist complete (#EXT-X-ENDLIST found)');
      }
    } else {
      console.info('[ProgressiveLoader] 💾 Using cached playlist for on-demand chunk', chunkIndex);
      levelDetails = trackChunks.levelDetails!;
    }

    // Check if chunk exists
    if (chunkIndex >= levelDetails.fragments.length) {
      console.warn('[ProgressiveLoader] ⚠️  Chunk index', chunkIndex, 'out of range (playlist has', levelDetails.fragments.length, 'fragments)');
      return null;
    }

    const fragment = levelDetails.fragments[chunkIndex];
    const startByte = fragment.byteRangeStartOffset!;
    const endByte = fragment.byteRangeEndOffset!;
    const chunkSize = endByte - startByte;
    console.info('[ProgressiveLoader] 📥 Fetching on-demand chunk', chunkIndex, '(' + (chunkSize / 1024).toFixed(2), 'KB)');

    const fetchStart = performance.now();
    const chunkData = await this.fetchByteRange(
      trackChunks.mediaUrl,
      trackId,
      startByte,
      chunkSize
    );
    const fetchTime = performance.now() - fetchStart;

    const fullSegment = this.concatenateBuffers(trackChunks.initSegment!, chunkData);

    console.info('[ProgressiveLoader] 🔊 Decoding on-demand chunk', chunkIndex);
    const decodeStart = performance.now();
    const audioBuffer = await this.audioContext.decodeAudioData(fullSegment);
    const decodeTime = performance.now() - decodeStart;

    console.info('[ProgressiveLoader] ✓ On-demand chunk', chunkIndex, 'ready in', (fetchTime + decodeTime).toFixed(0), 'ms (fetch:', fetchTime.toFixed(0), 'ms, decode:', decodeTime.toFixed(0), 'ms)');

    // Cache it
    trackChunks.chunks.set(chunkIndex, {
      audioBuffer,
      fragmentIndex: chunkIndex,
      timestamp: Date.now(),
    });

    this.pruneChunkCache(trackChunks);

    return audioBuffer;
  }

  /**
   * Prefetch chunks in background
   */
  async prefetchChunks(trackId: string, startIndex: number, count: number): Promise<void> {
    const trackChunks = this.trackCache.get(trackId);
    if (!trackChunks) {
      console.warn('[ProgressiveLoader] ⚠️  Cannot prefetch - no track data for:', trackId);
      return;
    }

    console.info('[ProgressiveLoader] 🔮 Prefetching', count, 'chunks starting from index', startIndex);

    const promises: Promise<void>[] = [];
    let skipped = 0;
    for (let i = startIndex; i < startIndex + count; i++) {
      if (trackChunks.chunks.has(i)) {
        skipped++;
        continue; // Already cached
      }

      promises.push(
        this.getChunk(trackId, i).catch(err => {
          console.warn('[ProgressiveLoader] ⚠️  Prefetch failed for chunk', i, err);
        }).then(() => {})
      );
    }

    console.info('[ProgressiveLoader] Prefetching', promises.length, 'chunks (' + skipped + ' already cached)');
    await Promise.all(promises);
    console.info('[ProgressiveLoader] ✓ Prefetch complete');
  }

  /**
   * Get playlist URL from backend
   */
  private async getPlaylistUrl(trackId: string): Promise<string> {
    const response = await fetch(`${baseUrl}/api/library/tracks/${trackId}/stream`);
    const data = await response.json();
    return data.link;
  }

  /**
   * Fetch and parse playlist
   */
  private async fetchAndParsePlaylist(url: string): Promise<LevelDetails> {
    const playlistText = await fetch(url).then(r => r.text());

    const levelDetails = M3U8Parser.parseLevelPlaylist(
      playlistText,
      url,
      0, // id
      'EVENT', // type
      0, // levelUrlId
      null // multivariantPlaylist
    );

    return levelDetails;
  }

  /**
   * Fetch byte range from media file
   */
  private async fetchByteRange(
    url: string,
    trackId: string,
    start: number,
    length: number
  ): Promise<ArrayBuffer> {
    const controller = new AbortController();
    const fetchKey = `${trackId}-${start}`;
    this.abortControllers.set(fetchKey, controller);

    try {
      const end = start + length - 1;
      const response = await fetch(url, {
        headers: {
          'Range': `bytes=${start}-${end}`,
        },
        signal: controller.signal,
      });

      if (!response.ok) {
        throw new Error(`Failed to fetch byte range: ${response.status}`);
      }

      const arrayBuffer = await response.arrayBuffer();
      return arrayBuffer;
    } finally {
      this.abortControllers.delete(fetchKey);
    }
  }

  /**
   * Concatenate init segment + chunk data
   */
  private concatenateBuffers(buffer1: ArrayBuffer, buffer2: ArrayBuffer): ArrayBuffer {
    const combined = new Uint8Array(buffer1.byteLength + buffer2.byteLength);
    combined.set(new Uint8Array(buffer1), 0);
    combined.set(new Uint8Array(buffer2), buffer1.byteLength);
    return combined.buffer;
  }

  /**
   * Prune chunk cache to keep only last N chunks
   */
  private pruneChunkCache(trackChunks: TrackChunks): void {
    if (trackChunks.chunks.size <= this.MAX_CHUNKS_PER_TRACK) {
      return;
    }

    // Sort by timestamp (LRU)
    const entries = Array.from(trackChunks.chunks.entries()).sort((a, b) => {
      return a[1].timestamp - b[1].timestamp;
    });

    // Remove oldest chunks
    const toRemove = entries.length - this.MAX_CHUNKS_PER_TRACK;
    console.info('[ProgressiveLoader] 🗑️  Cache full (' + trackChunks.chunks.size + '/' + this.MAX_CHUNKS_PER_TRACK + '), evicting', toRemove, 'oldest chunks');

    const evictedChunks: number[] = [];
    for (let i = 0; i < toRemove; i++) {
      const [chunkIndex] = entries[i];
      evictedChunks.push(chunkIndex);
      trackChunks.chunks.delete(chunkIndex);
    }

    console.info('[ProgressiveLoader] ✓ Evicted chunks:', evictedChunks.join(', '));
  }

  /**
   * Cancel all pending fetches for a track
   */
  cancelTrack(trackId: string): void {
    const keysToCancel = Array.from(this.abortControllers.keys()).filter(key =>
      key.startsWith(trackId)
    );

    for (const key of keysToCancel) {
      const controller = this.abortControllers.get(key);
      if (controller) {
        controller.abort();
        this.abortControllers.delete(key);
      }
    }
  }

  /**
   * Clear cache for a specific track
   */
  clearTrackCache(trackId: string): void {
    this.cancelTrack(trackId);
    this.trackCache.delete(trackId);
    console.info('[ProgressiveLoader] Cleared cache for track:', trackId);
  }

  /**
   * Clear all caches
   */
  clearAllCaches(): void {
    this.abortControllers.forEach(controller => controller.abort());
    this.abortControllers.clear();
    this.trackCache.clear();
    console.info('[ProgressiveLoader] Cleared all caches');
  }

  /**
   * Get cached chunk count for a track
   */
  getCachedChunkCount(trackId: string): number {
    const trackChunks = this.trackCache.get(trackId);
    return trackChunks?.chunks.size || 0;
  }

  /**
   * Calculate which chunk contains a specific time position
   */
  async getChunkIndexForPosition(trackId: string, position: number): Promise<number | null> {
    const trackChunks = this.trackCache.get(trackId);
    if (!trackChunks) return null;

    // Only refetch if playlist is not complete or we don't have levelDetails cached
    let levelDetails: LevelDetails;
    if (!trackChunks.isPlaylistComplete || !trackChunks.levelDetails) {
      console.info('[ProgressiveLoader] 📋 Refetching playlist for chunk index lookup');
      levelDetails = await this.fetchAndParsePlaylist(trackChunks.playlistUrl);
      trackChunks.levelDetails = levelDetails; // Cache latest playlist metadata

      // Mark playlist as complete if we see #EXT-X-ENDLIST
      if (!levelDetails.live && !trackChunks.isPlaylistComplete) {
        trackChunks.isPlaylistComplete = true;
        console.info('[ProgressiveLoader] ✅ Playlist complete (#EXT-X-ENDLIST found)');
      }
    } else {
      console.info('[ProgressiveLoader] 💾 Using cached playlist for chunk index lookup');
      levelDetails = trackChunks.levelDetails;
    }

    // Find fragment that contains this position
    let accumulatedTime = 0;
    for (let i = 0; i < levelDetails.fragments.length; i++) {
      const fragment = levelDetails.fragments[i];
      const fragmentDuration = fragment.duration;

      if (position >= accumulatedTime && position < accumulatedTime + fragmentDuration) {
        return i;
      }

      accumulatedTime += fragmentDuration;
    }

    // Return last chunk if position is at/beyond end
    return Math.max(0, levelDetails.fragments.length - 1);
  }

  /**
   * Get total track duration from HLS playlist
   */
  getTrackDuration(trackId: string): number | null {
    const trackChunks = this.trackCache.get(trackId);
    if (!trackChunks || !trackChunks.levelDetails) return null;

    // Sum all fragment durations
    return trackChunks.levelDetails.fragments.reduce((total, fragment) => {
      return total + fragment.duration;
    }, 0);
  }

  /**
   * Check if track playlist is complete (has #EXT-X-ENDLIST)
   */
  isPlaylistComplete(trackId: string): boolean {
    const trackChunks = this.trackCache.get(trackId);
    if (!trackChunks || !trackChunks.levelDetails) return false;

    // Playlist is complete when live is false (has #EXT-X-ENDLIST)
    return !trackChunks.levelDetails.live;
  }

  /**
   * Get cumulative time before a specific chunk index
   */
  getCumulativeTimeBeforeChunk(trackId: string, chunkIndex: number): number | null {
    const trackChunks = this.trackCache.get(trackId);
    if (!trackChunks || !trackChunks.levelDetails) return null;

    let cumulativeTime = 0;
    for (let i = 0; i < chunkIndex && i < trackChunks.levelDetails.fragments.length; i++) {
      cumulativeTime += trackChunks.levelDetails.fragments[i].duration;
    }

    return cumulativeTime;
  }
}
