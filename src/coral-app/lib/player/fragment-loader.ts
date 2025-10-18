import { baseUrl } from '@/lib/client/fetcher';
import M3U8Parser from '@/lib/vendor/hls.js/src/loader/m3u8-parser';
import type { LevelDetails } from '@/lib/vendor/hls.js/src/loader/level-details';

export interface StreamInfo {
  codec: string;
  playlistUrl: string;
  levelDetails: LevelDetails;
  mediaUrl: string;
  duration: number;
}

/**
 * FragmentLoader
 *
 * Handles fetching audio fragment data.
 * Responsibilities:
 * - Fetching stream info from API
 * - Parsing M3U8 playlists
 * - Fetching byte ranges for fMP4 segments
 */
export class FragmentLoader {
  /**
   * Fetch stream information for a track from the API
   */
  async fetchStreamInfo(trackId: string): Promise<StreamInfo> {
    const streamInfoResponse = await fetch(`${baseUrl}/api/library/tracks/${trackId}/stream`);
    if (!streamInfoResponse.ok) {
      throw new Error(`Failed to get stream info: ${streamInfoResponse.status}`);
    }

    const streamData = await streamInfoResponse.json();
    const codec = streamData.transcodeInfo?.codec;

    if (!codec) {
      throw new Error('Codec information not available from API');
    }

    const playlistUrl = streamData.link;
    const levelDetails = await this.fetchAndParsePlaylist(playlistUrl);
    const mediaUrl = levelDetails.fragments[0]?.url || '';

    const duration = levelDetails.fragments.reduce((total, fragment) => {
      return total + fragment.duration;
    }, 0);

    return { codec, playlistUrl, levelDetails, mediaUrl, duration };
  }

  /**
   * Fetch and parse an M3U8 playlist
   */
  async fetchAndParsePlaylist(url: string): Promise<LevelDetails> {
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

  /**
   * Fetch a byte range from a media URL
   */
  async fetchByteRange(url: string, start: number, length: number): Promise<ArrayBuffer> {
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

  /**
   * Fetch init segment data
   */
  async fetchInitSegment(mediaUrl: string, byteRangeEnd: number, codec: string): Promise<ArrayBuffer> {
    return await this.fetchByteRange(mediaUrl, 0, byteRangeEnd);
  }

  /**
   * Fetch and process a fragment
   */
  async fetchFragment(
    fragmentUrl: string,
    byteRangeStart: number,
    byteRangeEnd: number,
    codec: string
  ): Promise<ArrayBuffer> {
    const size = byteRangeEnd - byteRangeStart;
    return await this.fetchByteRange(fragmentUrl, byteRangeStart, size);
  }
}
