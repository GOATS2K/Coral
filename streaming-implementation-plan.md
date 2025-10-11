# Progressive fMP4 Streaming Implementation Plan

**Last Updated:** 2025-10-11
**Status:** Planning Complete - Ready for Implementation
**Integration:** Extends existing `TranscoderService` (no new service needed)

---

## Executive Summary

This plan adds progressive fMP4 streaming to Coral's web player to enable instant playback of long audio tracks without waiting for full file download. The implementation integrates with the existing `TranscoderService` and `Coral.Encoders` infrastructure, maintaining architectural consistency.

### Key Goals
- ✅ Instant playback (no full file download required)
- ✅ Sample-accurate gapless playback (Web Audio API)
- ✅ Format-preserving remux (FLAC/AAC/MP3 → fMP4 without re-encoding)
- ✅ Progressive chunk loading with memory management
- ✅ Backwards compatible with existing HLS transcoding

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         BACKEND (C#)                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Controller → TranscoderService → EncoderFactory → RemuxFFmpeg  │
│                                         ↓                        │
│                              FFmpegRemuxBuilder                  │
│                                         ↓                        │
│                     GetProgressiveHlsPipeCommand()               │
│                                         ↓                        │
│                  Single fMP4 + byte-range playlist               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      FRONTEND (TypeScript)                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  WebAudioPlayer → ProgressiveWebAudioPlayer                      │
│                           ↓                                      │
│           Fetch playlist.m3u8 → Parse with hls.js                │
│                           ↓                                      │
│           Fetch init segment (Range: bytes=0-X)                  │
│                           ↓                                      │
│       Progressive chunk fetch → Decode → Schedule → Play         │
│                           ↓                                      │
│              Memory management (keep ±1 chunks)                  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Implementation Stages

### Stage 1: Technical Validation (3 tasks)
**Goal:** Verify technical assumptions before writing production code

1. **Verify hls.js M3U8Parser import**
   - Test: `import M3U8Parser from 'hls.js/src/loader/m3u8-parser'`
   - Fallback: Manual m3u8 parsing or vendor parser code
   - Success criteria: Can parse m3u8 and extract byte ranges

2. **Test FFmpeg FLAC → fMP4 (no bitstream filter)**
   ```bash
   ffmpeg -i input.flac -c:a copy -movflags frag_keyframe+empty_moov \
     -frag_duration 10000000 -f hls -hls_playlist_type event \
     -hls_segment_type fmp4 -hls_fmp4_init_filename "" \
     -hls_flags single_file playlist.m3u8
   ```
   - Success criteria: Single .m4a file + playlist with byte ranges
   - Verify: NO `-bsf:a` flag used

3. **Test FFmpeg AAC → fMP4 (with aac_adtstoasc filter)**
   ```bash
   # Same as above, but add:
   -bsf:a aac_adtstoasc
   ```
   - Success criteria: AAC properly packaged in fMP4
   - Verify: Filter is required for AAC

**Stage 1 Exit Criteria:** All FFmpeg commands produce valid playlists with byte-range segments

---

### Stage 2: Backend Foundation (3 tasks)
**Goal:** Add base infrastructure to support remuxing

**2.1. Add `Remux` to OutputFormat enum**

**Location:** `src/Coral.Dto/EncodingModels/HelperEnums.cs`

```csharp
public enum OutputFormat
{
    AAC, MP3, Ogg, Opus,
    Remux  // NEW: Format-preserving remux to fMP4
}
```

**2.2. Add `Progressive` to TranscodeRequestType enum**

**Location:** `src/Coral.Dto/EncodingModels/TranscodingJobRequest.cs`

```csharp
public enum TranscodeRequestType
{
    SingleFile,
    HLS,
    Progressive  // NEW: Progressive fMP4 with byte-range playlists
}
```

**2.3. Extend IArgumentBuilder interface**

**Location:** `src/Coral.Encoders/IArgumentBuilder.cs`

```csharp
public interface IArgumentBuilder
{
    IArgumentBuilder SetBitrate(int value);
    IArgumentBuilder SetSourceFile(string path);
    IArgumentBuilder SetDestinationFile(string path);
    IArgumentBuilder GenerateHLSStream();

    // NEW: Progressive streaming methods
    IArgumentBuilder GenerateProgressiveStream();
    IArgumentBuilder SetFragmentDuration(int seconds);

    string[] BuildArguments();
}
```

**Stage 2 Exit Criteria:** Solution builds successfully with new enums and interface methods

---

### Stage 3: Backend Remux Implementation (4 tasks)
**Goal:** Implement remux encoder integrated with Coral.Encoders

**3.1. Create FFmpegRemuxBuilder**

**Location:** `src/Coral.Encoders/Remux/FFmpegRemuxBuilder.cs`

Key responsibilities:
- Detect source codec from file extension
- Output to stdout for piping (`-f [format] -`)
- Support progressive mode flag
- No bitrate setting (copy codec)

```csharp
namespace Coral.Encoders.Remux;

public class FFmpegRemuxBuilder : IArgumentBuilder
{
    private string? _sourceCodec = null;
    private bool _progressiveMode = false;

    public IArgumentBuilder SetSourceFile(string path)
    {
        // Detect codec: flac, mp3, aac, opus, alac, pcm
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        _sourceCodec = ext switch
        {
            "flac" => "flac",
            "mp3" => "mp3",
            "m4a" or "aac" => "aac",
            "opus" => "opus",
            "alac" => "alac",
            "aiff" or "aif" => "pcm",
            _ => null
        };
        return this;
    }

    public IArgumentBuilder GenerateProgressiveStream()
    {
        _progressiveMode = true;

        // Output format to stdout
        if (_sourceCodec == "alac" || _sourceCodec == "pcm")
        {
            // Transcode to FLAC (not supported in fMP4)
            _arguments.Add("-c:a");
            _arguments.Add("flac");
            _arguments.Add("-f");
            _arguments.Add("flac");
        }
        else if (_sourceCodec == "aac")
        {
            _arguments.Add("-f");
            _arguments.Add("adts");  // AAC ADTS format
        }
        else
        {
            // FLAC, MP3, Opus - direct output
            _arguments.Add("-f");
            _arguments.Add(_sourceCodec ?? "flac");
        }

        SetDestinationFile("-");
        return this;
    }

    // ... rest of implementation
}
```

**3.2. Create RemuxFFmpeg encoder**

**Location:** `src/Coral.Encoders/Remux/FFmpeg.cs`

```csharp
namespace Coral.Encoders.Remux;

[EncoderFrontend(nameof(RemuxFFmpeg), OutputFormat.Remux,
    Platform.Windows, Platform.Linux, Platform.MacOS)]
public class RemuxFFmpeg : IEncoder
{
    public string ExecutableName => "ffmpeg";
    public bool WritesOutputToStdErr => false;

    public IArgumentBuilder Configure()
    {
        return new FFmpegRemuxBuilder();
    }

    public override TranscodingJob ConfigureTranscodingJob(TranscodingJobRequest request)
    {
        var job = new TranscodingJob { Request = request };

        var configuration = Configure()
            .SetSourceFile(request.SourceTrack.AudioFile.FilePath);

        if (request.RequestType == TranscodeRequestType.Progressive)
        {
            configuration.GenerateProgressiveStream();
            job.OutputDirectory = Path.Combine(
                ApplicationConfiguration.HLSDirectory,
                "streaming",
                request.SourceTrack.Id.ToString()
            );
            job.FinalOutputFile = "playlist.m3u8";

            // Pipe to progressive HLS generator
            job.PipeCommand = CommonEncoderMethods.GetProgressiveHlsPipeCommand(
                job,
                GetSourceCodec(request.SourceTrack.AudioFile.FilePath)
            );
        }

        job.TranscodingCommand = Cli.Wrap(ExecutableName)
            .WithArguments(configuration.BuildArguments());

        return job;
    }

    private string GetSourceCodec(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "flac" => "flac",
            "mp3" => "mp3",
            "m4a" or "aac" => "aac",
            "opus" => "opus",
            _ => "flac"
        };
    }
}
```

**3.3. Add GetProgressiveHlsPipeCommand() to CommonEncoderMethods**

**Location:** `src/Coral.Encoders/CommonEncoderMethods.cs`

```csharp
public static Command GetProgressiveHlsPipeCommand(TranscodingJob job, string sourceCodec)
{
    var jobDir = Path.Combine(
        ApplicationConfiguration.HLSDirectory,
        "streaming",
        job.Request.SourceTrack.Id.ToString()
    );
    Directory.CreateDirectory(jobDir);

    var fmp4File = Path.Combine(jobDir, $"track_{job.Request.SourceTrack.Id}.m4a");
    var playlistFile = Path.Combine(jobDir, "playlist.m3u8");

    var args = new List<string>
    {
        "-i", "-",  // Read from stdin
        "-loglevel", "error",
        "-hide_banner",
        "-c:a", "copy",  // Copy codec (no re-encode)
        "-movflags", "frag_keyframe+empty_moov+default_base_moof",
        "-frag_duration", "10000000",  // 10 seconds
        "-f", "hls",
        "-hls_time", "10",
        "-hls_playlist_type", "event",
        "-hls_segment_type", "fmp4",
        "-hls_fmp4_init_filename", "",  // No separate init file
        "-hls_flags", "single_file",
    };

    // CRITICAL: Format-specific bitstream filter
    // NO filter for FLAC/MP3/Opus! Only AAC needs aac_adtstoasc
    if (sourceCodec == "aac")
    {
        args.Add("-bsf:a");
        args.Add("aac_adtstoasc");
    }

    args.Add("-master_pl_name");
    args.Add("master.m3u8");
    args.Add(playlistFile);

    return Cli.Wrap("ffmpeg")
        .WithValidation(CommandResultValidation.ZeroExitCode)
        .WithArguments(args);
}
```

**3.4. Test encoder discovery**

Verify `EncoderFactory.GetEncoder(OutputFormat.Remux)` finds `RemuxFFmpeg`.

**Stage 3 Exit Criteria:**
- Encoder builds and is discovered by EncoderFactory
- Can create TranscodingJob with Progressive request type
- FFmpeg commands are correctly formatted per source codec

---

### Stage 4: Backend Service Integration (4 tasks)
**Goal:** Integrate with existing TranscoderService and expose API endpoint

**4.1. Extend TranscoderService (NO NEW SERVICE NEEDED)**

The existing `TranscoderService` already handles:
- ✅ Creating jobs with `EncoderFactory.GetEncoder(format)`
- ✅ Executing transcoding commands with pipes
- ✅ Waiting for output files
- ✅ Caching jobs by track ID + bitrate

**No changes to TranscoderService needed.** Just call it with:
```csharp
await _transcoderService.CreateJob(OutputFormat.Remux, req =>
{
    req.SourceTrack = track;
    req.RequestType = TranscodeRequestType.Progressive;
    req.Bitrate = 0;  // Not used for remux
});
```

**4.2. Add progressive streaming endpoint**

**Option A:** Extend existing stream endpoint with query parameter

Find the controller with `/api/library/tracks/{trackId}/stream` and add:

```csharp
[HttpGet("tracks/{trackId}/stream")]
public async Task<IActionResult> StreamTrack(
    int trackId,
    [FromQuery] bool progressive = false)
{
    var track = await _db.Tracks
        .Include(t => t.AudioFile)
        .FirstOrDefaultAsync(t => t.Id == trackId);

    if (track?.AudioFile == null)
        return NotFound();

    if (progressive)
    {
        var job = await _transcoderService.CreateJob(OutputFormat.Remux, req =>
        {
            req.SourceTrack = track;
            req.RequestType = TranscodeRequestType.Progressive;
            req.Bitrate = 0;
        });

        var playlistUrl = $"/hls/streaming/{trackId}/playlist.m3u8";
        return Ok(new { playlistUrl });
    }

    // ... existing transcode logic ...
}
```

**Option B:** Separate endpoint

```csharp
[HttpGet("tracks/{trackId}/stream/progressive")]
public async Task<IActionResult> StreamTrackProgressive(int trackId)
{
    // Same logic as above
}
```

**4.3. Test end-to-end playlist generation**

```bash
# Test with FLAC file
curl "http://localhost:7214/api/library/tracks/123/stream?progressive=true"
# Should return: { "playlistUrl": "/hls/streaming/123/playlist.m3u8" }

# Fetch playlist
curl "http://localhost:7214/hls/streaming/123/playlist.m3u8"
# Should contain: #EXT-X-BYTERANGE tags and event playlist type
```

**4.4. Verify HTTP Range request support**

```bash
# Fetch init segment (bytes 0-X from playlist)
curl -H "Range: bytes=0-1234" \
  "http://localhost:7214/hls/streaming/123/track_123.m4a"
# Should return: 206 Partial Content
```

**Stage 4 Exit Criteria:**
- API endpoint returns playlist URL
- Playlist file contains byte-range segments
- Range requests return 206 Partial Content
- Single .m4a file generated (not multiple segments)

---

### Stage 5: Frontend Investigation (2 tasks)
**Goal:** Validate frontend dependencies and parsing

**5.1. Install hls.js and verify M3U8Parser import**

```bash
cd src/coral-app
bun add hls.js
```

Create test file `src/coral-app/test-hls-parser.ts`:
```typescript
import M3U8Parser from 'hls.js/src/loader/m3u8-parser';

const parser = new M3U8Parser();
console.log('M3U8Parser loaded:', typeof parser.parseLevelPlaylist);
```

Run: `bun run test-hls-parser.ts`

**Fallback plan if import fails:**
- Check hls.js exports/types
- Consider manual m3u8 parsing (regex-based)
- Vendor parser code if needed

**5.2. Create test script to parse playlist**

Once backend generates a playlist, test parsing:

```typescript
// test-playlist-parse.ts
import M3U8Parser from 'hls.js/src/loader/m3u8-parser';

const playlistUrl = 'http://localhost:7214/hls/streaming/123/playlist.m3u8';
const playlistText = await fetch(playlistUrl).then(r => r.text());

const parser = new M3U8Parser();
const levelDetails = parser.parseLevelPlaylist(
  playlistText,
  playlistUrl,
  0,
  'MAIN',
  0,
  null
);

console.log('Fragments:', levelDetails.fragments.length);
console.log('Init segment byte range:', levelDetails.fragments[0]?.initSegment?.byteRange);
console.log('First chunk byte range:', levelDetails.fragments[0]?.byteRange);
```

**Stage 5 Exit Criteria:**
- hls.js installed successfully
- M3U8Parser imports and parses playlists
- Byte ranges extracted correctly

---

### Stage 6: Frontend Progressive Player (7 tasks)
**Goal:** Implement core progressive playback logic

**6.1. Create ProgressiveWebAudioPlayer class skeleton**

**Location:** `src/coral-app/lib/player/progressive-web-audio-player.ts`

```typescript
export class ProgressiveWebAudioPlayer {
  private audioContext: AudioContext;
  private baseUrl: string = '';
  private fmp4Url: string = '';
  private initSegmentData: ArrayBuffer | null = null;
  private chunks: ChunkMetadata[] = [];
  private decodedChunks: Map<number, DecodedChunk> = new Map();
  private currentChunkIndex: number = 0;

  constructor(audioContext: AudioContext) {
    this.audioContext = audioContext;
  }

  async loadTrack(trackId: number): Promise<void> { /* TODO */ }
  async play(startTime: number = 0): Promise<void> { /* TODO */ }
  stop(): void { /* TODO */ }
  seek(time: number): void { /* TODO */ }
  getCurrentTime(): number { /* TODO */ }
  getTotalDuration(): number { /* TODO */ }
}
```

**6.2. Implement loadTrack() with playlist fetch and parsing**

```typescript
async loadTrack(trackId: number): Promise<void> {
  // 1. Get playlist URL from backend
  const response = await fetch(`/api/library/tracks/${trackId}/stream?progressive=true`);
  const { playlistUrl } = await response.json();

  // 2. Fetch playlist
  const playlistResponse = await fetch(playlistUrl);
  const playlistText = await playlistResponse.text();

  // 3. Parse with hls.js
  const parser = new M3U8Parser();
  const levelDetails = parser.parseLevelPlaylist(playlistText, playlistUrl, 0, 'MAIN', 0, null);

  // 4. Extract init segment and chunks
  const initSegment = levelDetails.fragments[0]?.initSegment;
  this.fmp4Url = initSegment.url;
  this.chunks = levelDetails.fragments.map((frag, index) => ({
    index,
    byteRange: frag.byteRange as [number, number],
    duration: frag.duration,
    startTime: frag.start,
  }));

  // 5. Fetch init segment
  await this.fetchInitSegment(initSegment.byteRange as [number, number]);
}
```

**6.3. Implement init segment fetch and caching**

```typescript
private async fetchInitSegment(byteRange: [number, number]): Promise<void> {
  const response = await fetch(this.fmp4Url, {
    headers: { Range: `bytes=${byteRange[0]}-${byteRange[1] - 1}` },
  });
  this.initSegmentData = await response.arrayBuffer();
}
```

**6.4. Implement progressive chunk fetching and decoding**

```typescript
private async fetchChunk(chunkIndex: number): Promise<ArrayBuffer> {
  const chunk = this.chunks[chunkIndex];
  const response = await fetch(this.fmp4Url, {
    headers: { Range: `bytes=${chunk.byteRange[0]}-${chunk.byteRange[1] - 1}` },
  });
  return await response.arrayBuffer();
}

private async decodeChunk(chunkIndex: number): Promise<DecodedChunk> {
  const cached = this.decodedChunks.get(chunkIndex);
  if (cached) return cached;

  const chunk = this.chunks[chunkIndex];
  const chunkData = await this.fetchChunk(chunkIndex);

  // Concatenate init segment + chunk
  const combined = new Uint8Array(
    this.initSegmentData!.byteLength + chunkData.byteLength
  );
  combined.set(new Uint8Array(this.initSegmentData!), 0);
  combined.set(new Uint8Array(chunkData), this.initSegmentData!.byteLength);

  // Decode
  const audioBuffer = await this.audioContext.decodeAudioData(combined.buffer);

  const decoded: DecodedChunk = {
    index: chunkIndex,
    buffer: audioBuffer,
    startTime: chunk.startTime,
    duration: chunk.duration,
  };

  this.decodedChunks.set(chunkIndex, decoded);
  this.cleanupOldChunks(chunkIndex);

  return decoded;
}
```

**6.5. Implement play() with chunk scheduling and pre-fetching**

```typescript
async play(startTime: number = 0): Promise<void> {
  const chunkIndex = this.chunks.findIndex(
    chunk => startTime >= chunk.startTime && startTime < chunk.startTime + chunk.duration
  );

  this.currentChunkIndex = chunkIndex;
  await this.scheduleChunk(chunkIndex, startTime);
}

private async scheduleChunk(chunkIndex: number, startTime: number): Promise<void> {
  const decoded = await this.decodeChunk(chunkIndex);
  const chunk = this.chunks[chunkIndex];
  const offsetInChunk = startTime - chunk.startTime;

  const source = this.audioContext.createBufferSource();
  source.buffer = decoded.buffer;
  source.connect(this.audioContext.destination);

  source.onended = () => {
    if (chunkIndex + 1 < this.chunks.length) {
      const nextChunk = this.chunks[chunkIndex + 1];
      this.scheduleChunk(chunkIndex + 1, nextChunk.startTime);
    }
  };

  source.start(this.audioContext.currentTime, offsetInChunk);

  // Pre-fetch next chunk
  if (chunkIndex + 1 < this.chunks.length) {
    this.decodeChunk(chunkIndex + 1).catch(err =>
      console.error('[Progressive] Pre-fetch failed:', err)
    );
  }
}
```

**6.6. Implement memory management (chunk cleanup)**

```typescript
private cleanupOldChunks(currentIndex: number): void {
  // Keep only currentIndex ± 1 (3 chunks total)
  const toKeep = new Set([
    currentIndex - 1,
    currentIndex,
    currentIndex + 1,
  ]);

  for (const [index, _] of this.decodedChunks) {
    if (!toKeep.has(index)) {
      this.decodedChunks.delete(index);
    }
  }
}
```

**6.7. Implement accurate getCurrentTime() tracking**

```typescript
private playbackStartTime: number = 0;
private playbackStartContextTime: number = 0;

async play(startTime: number = 0): Promise<void> {
  this.playbackStartTime = startTime;
  this.playbackStartContextTime = this.audioContext.currentTime;
  // ... rest of play logic
}

getCurrentTime(): number {
  const elapsed = this.audioContext.currentTime - this.playbackStartContextTime;
  return this.playbackStartTime + elapsed;
}
```

**6.8. Implement seek() support**

```typescript
seek(time: number): void {
  this.stop();
  this.play(time);
}
```

**Stage 6 Exit Criteria:**
- Can load playlist and parse byte ranges
- Progressive playback starts instantly (no full download)
- Chunks chain together seamlessly
- Memory usage stays bounded (only 3 chunks in memory)
- Seeking works accurately

---

### Stage 7: Frontend Integration (3 tasks)
**Goal:** Integrate with existing WebAudioPlayer

**7.1. Add progressive player integration to WebAudioPlayer**

**Location:** `src/coral-app/lib/player/web-audio-player.ts`

```typescript
import { ProgressiveWebAudioPlayer } from './progressive-web-audio-player';

private progressivePlayer: ProgressiveWebAudioPlayer | null = null;
private readonly PROGRESSIVE_THRESHOLD_SECONDS = 300; // 5 minutes

async scheduleTrack(trackIndex: number, scheduleTime: number): Promise<void> {
  const track = this.tracks[trackIndex];

  // Check duration threshold
  if (track.duration && track.duration > this.PROGRESSIVE_THRESHOLD_SECONDS) {
    return this.scheduleProgressiveTrack(trackIndex, scheduleTime);
  }

  // ... existing direct download logic ...
}

private async scheduleProgressiveTrack(trackIndex: number, scheduleTime: number): Promise<void> {
  const track = this.tracks[trackIndex];

  if (!this.progressivePlayer) {
    this.progressivePlayer = new ProgressiveWebAudioPlayer(this.audioContext);
  }

  try {
    await this.progressivePlayer.loadTrack(track.id);

    const delay = Math.max(0, scheduleTime - this.audioContext.currentTime);
    setTimeout(() => {
      this.progressivePlayer!.play(0);
    }, delay * 1000);
  } catch (error) {
    console.error('[WebAudio] Progressive failed:', error);
    // Fallback to direct download
    // ... call existing direct download logic ...
  }
}
```

**7.2. Implement duration threshold logic (5 minutes)**

Already shown above - tracks > 5 minutes use progressive, others use direct download.

**7.3. Add fallback to direct download on progressive failure**

Already shown above - catch block falls back to existing direct download logic.

**Stage 7 Exit Criteria:**
- Long tracks (>5min) automatically use progressive streaming
- Short tracks continue using direct download
- Failures gracefully fall back to direct download

---

### Stage 8: Testing & Refinement (8 tasks)
**Goal:** Comprehensive testing and edge case handling

**8.1. Test FLAC progressive playback (>5min track)**
- Verify instant playback start
- Check gapless playback across chunks
- Monitor memory usage

**8.2. Test AAC/MP3 progressive playback**
- Verify bitstream filter applied correctly
- Check browser compatibility (Chrome/Firefox/Safari)

**8.3. Test ALAC/AIFF transcode pipeline**
- Verify transcodes to FLAC first
- Check playback quality

**8.4. Test seeking accuracy**
- Seek to various positions (start, middle, end)
- Verify correct chunk selection
- Check offset calculation accuracy

**8.5. Test gapless transitions**
- Direct → Progressive
- Progressive → Direct
- Progressive → Progressive

Measure sample-accurate gaps using audioContext timing.

**8.6. Monitor memory usage during long playback sessions**
- Play 30-minute track
- Monitor decoded chunk count (should stay ≤ 3)
- Check for memory leaks (DevTools Memory Profiler)

**8.7. Test network error recovery**
- Simulate failed chunk fetch (Network throttle in DevTools)
- Verify retry logic or fallback behavior
- Test offline → online transition

**8.8. Browser compatibility testing**
- Chrome (primary)
- Firefox
- Safari (Web Audio API differences)
- Edge

**Stage 8 Exit Criteria:**
- All formats play correctly
- Gapless transitions work across all combinations
- Memory usage stays bounded
- Network errors handled gracefully
- Works in all major browsers

---

## Key Technical Decisions

### ✅ Single-File fMP4 with Byte-Range Playlists
- Uses `-hls_flags single_file` to generate ONE .m4a file
- Playlist contains `#EXT-X-BYTERANGE` tags
- Frontend uses HTTP Range requests for progressive loading
- Simpler than multi-segment HLS

### ✅ Format-Specific Bitstream Filters
- **FLAC/MP3/Opus:** NO bitstream filter (direct copy)
- **AAC:** Uses `aac_adtstoasc` filter for MP4 packaging
- **ALAC/AIFF:** Transcode to FLAC first (not supported in fMP4)

### ✅ Coral.Encoders Integration
- New `OutputFormat.Remux` enum value
- New `TranscodeRequestType.Progressive` enum value
- Uses `EncoderFactory` for platform-agnostic encoder discovery
- Follows existing `IEncoder` and `IArgumentBuilder` patterns

### ✅ TranscoderService Reuse
- **NO new service needed** - existing TranscoderService handles:
  - Job creation with EncoderFactory
  - Command execution with pipes
  - Output file waiting
  - Job caching by track ID + bitrate

### ✅ Web Audio API for Gapless Playback
- Manual scheduling with `AudioBufferSourceNode`
- Sample-accurate timing across chunk boundaries
- Avoids `<audio>` element timing inaccuracies

### ✅ Memory Management Strategy
- Keep only current chunk ± 1 (3 chunks total in memory)
- ~30 seconds of audio buffered (at 10-second chunks)
- Automatic cleanup on chunk advancement

---

## Open Questions & Future Work

1. **Adaptive Chunk Size**
   - Current: Fixed 10-second chunks
   - Future: Adjust based on bitrate/network speed

2. **Caching Strategy**
   - Should generated fMP4 files be cached long-term?
   - How to invalidate cache on library re-index?

3. **Look-Ahead Scheduling**
   - Current: Reactive `onended` callback
   - Future: Pre-schedule next chunk before current ends (eliminate gaps)

4. **Network Error Recovery**
   - Current: No retry logic
   - Future: Exponential backoff retry or fallback to direct download

5. **Position Tracking Refinement**
   - Current: Context time + offset calculation
   - Future: More precise tracking across chunk boundaries

6. **Progressive → Progressive Gapless**
   - Needs testing to ensure chunk alignment
   - May need final chunk prefetch for perfect alignment

---

## Success Metrics

- ✅ **Instant Playback:** Audio starts < 1 second after play button click
- ✅ **Gapless Playback:** < 1ms gaps between chunks (measured with audioContext)
- ✅ **Memory Efficiency:** Memory usage stays bounded regardless of track length
- ✅ **Format Support:** FLAC, AAC, MP3, Opus, ALAC, AIFF all work correctly
- ✅ **Backwards Compatible:** Existing HLS transcoding unaffected

---

## Files to Create/Modify

### New Files
- `src/Coral.Encoders/Remux/FFmpegRemuxBuilder.cs`
- `src/Coral.Encoders/Remux/FFmpeg.cs`
- `src/coral-app/lib/player/progressive-web-audio-player.ts`

### Modified Files
- `src/Coral.Dto/EncodingModels/HelperEnums.cs` (add Remux enum)
- `src/Coral.Dto/EncodingModels/TranscodingJobRequest.cs` (add Progressive enum)
- `src/Coral.Encoders/IArgumentBuilder.cs` (extend interface)
- `src/Coral.Encoders/CommonEncoderMethods.cs` (add GetProgressiveHlsPipeCommand)
- `src/coral-app/lib/player/web-audio-player.ts` (integrate progressive player)
- Appropriate controller (add progressive streaming endpoint)

---

**Implementation Start Date:** TBD
**Target Completion:** TBD
**Primary Developer:** TBD
