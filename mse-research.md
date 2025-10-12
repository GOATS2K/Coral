# MSE-Based Gapless Audio Player Research

## Problem Statement

### Current Implementation Issues

**Approach 1: Web Audio API with `decodeAudioData()`**
- ✅ Works perfectly for PCM formats (WAV, FLAC) - true gapless
- ❌ MP3/AAC introduce clicks at chunk boundaries
- **Root cause**: `decodeAudioData()` returns ALL decoded samples including encoder padding/delay, but provides no metadata to trim them

**Approach 2: Dual Audio Element Crossfading**
- ❌ DOM-level volume control: Inconsistent browser timing
- ❌ Web Audio API gain nodes: Still insufficient timing precision
- **Root cause**: Separate media elements have independent playback timelines; `play()`, `pause()`, and `ended` events have unpredictable latency

### Core Requirements

1. Gapless **streaming** audio playback (progressive loading)
2. Gapless **transitions** between tracks (no clicks or gaps)
3. **Instant startup** even with large audio files
4. Support for **mixed codecs** in playlists (AAC/MP3/FLAC)
5. **Web Audio API integration** for effects/visualization
6. **No transcoding** - preserve user's source file codecs

## Solution: Media Source Extensions (MSE)

### Architecture Overview

```
Backend serves audio files directly:
  MP3 → serve directly (already streamable)
  FLAC → serve directly (already streamable)
  M4A/AAC → remux with -movflags +faststart (moov atom at beginning)
           (or continue using current fMP4 HLS approach)

Frontend progressive streaming:
  fetch(audioUrl) → ReadableStream
       ↓
  Progressive chunks appended to SourceBuffer
       ↓
  Single <audio> element with MediaSource
       ↓
  Single persistent SourceBuffer (changeType() for codec switches)
       ↓
  MediaElementSourceNode → Web Audio API → destination
```

**Key Insight from Spotify & Modern MSE Examples**: We don't need HLS segmentation! MSE can progressively stream regular audio files. The browser buffers automatically as data arrives via `fetch()`.

### Why MSE Solves the Problems

1. **Single continuous SourceBuffer**: All tracks appended to the same buffer create seamless playback
2. **Precise padding removal**: `appendWindowStart/End` trim encoder padding at sample-accurate precision
3. **Single audio element**: No synchronization issues between multiple elements
4. **Progressive streaming**: Append chunks as they arrive, instant playback without full download
5. **Codec flexibility**: `changeType()` enables mid-stream codec switching without recreating MediaSource
6. **Simpler than HLS**: No playlist parsing, no multi-file fetching, just `fetch()` + `appendBuffer()`

## How Cross-Track Gapless Playback Works

This is the **core magic** of the MSE approach - understanding how multiple tracks play back seamlessly.

### The Single SourceBuffer Timeline

Unlike traditional audio players that use separate `<audio>` elements (which have independent timelines), MSE uses a **single continuous SourceBuffer** that spans all tracks:

```
Time:     0s          120s         240s         360s
          |------------|------------|------------|
SourceBuffer: [Track 1 FLAC][Track 2 MP3][Track 3 AAC]

Audio Element: Plays continuously from 0s → 360s with no gaps
```

**Key Concept**: When the audio element's `currentTime` reaches 120 seconds, it seamlessly continues into Track 2's audio data without any gap or discontinuity because it's all in the same buffer.

### Step-by-Step Gapless Transition

Here's exactly what happens when transitioning from Track 1 (FLAC) to Track 2 (MP3):

#### 1. **Track 1 is Playing** (0s - 120s)
```typescript
// Track 1 already appended to SourceBuffer
sourceBuffer.mode = 'sequence';
// FLAC data at timeline position 0-120s
audioElement.currentTime // approaching 119.9s...
```

#### 2. **Append Track 2 Before Track 1 Ends**
```typescript
// When Track 1 hits ~100 seconds (buffer ahead)
const track2Codec = 'audio/mp4; codecs="mp3"';
const track1Codec = 'audio/mp4; codecs="flac"';

// Change codec type (if different from Track 1)
if (track2Codec !== track1Codec) {
  sourceBuffer.changeType(track2Codec); // Switch decoder to MP3
}

// Configure timeline position for Track 2
const track1Duration = 120; // seconds
const track2EncoderDelay = 0.025057; // from gapless metadata
const track2Padding = 0.012; // from gapless metadata
const track2RealDuration = 240 - track2EncoderDelay - track2Padding;

// Set append window to trim Track 2's encoder padding
sourceBuffer.appendWindowStart = 120; // Start right after Track 1
sourceBuffer.appendWindowEnd = 120 + track2RealDuration;
sourceBuffer.timestampOffset = 120 - track2EncoderDelay;

// Fetch and append Track 2 data progressively
const response = await fetch(track2Url);
const reader = response.body.getReader();

while (true) {
  const { done, value } = await reader.read();
  if (done) break;

  // Wait for SourceBuffer to be ready
  if (sourceBuffer.updating) {
    await waitForUpdateEnd(sourceBuffer);
  }

  sourceBuffer.appendBuffer(value); // Append MP3 chunk
  await waitForUpdateEnd(sourceBuffer);
}

// Now SourceBuffer contains: [Track 1: 0-120s][Track 2: 120-360s]
```

#### 3. **The Seamless Transition** (at 120s)
```typescript
// Audio element continues playing
audioElement.currentTime // 119.98s... 119.99s... 120.00s... 120.01s...
// NO GAP! Audio element doesn't know tracks changed
// It just sees continuous audio data in the SourceBuffer

// What the user hears:
// 119.9s: FLAC audio (last few samples)
// 120.0s: MP3 audio (first sample, padding already trimmed by appendWindow)
// No click, no gap, perfect transition
```

### Why This Works

1. **Single Timeline**: The audio element has one playback timeline (0 → infinity)
2. **Continuous Buffer**: All track data lives in the same SourceBuffer, positioned sequentially
3. **`changeType()` Preserves Timeline**: Switching codecs doesn't reset the timeline or create a new buffer
4. **Append Windows Trim Padding**: `appendWindowStart/End` ensure only "real" audio samples are kept, no encoder padding
5. **`timestampOffset` Aligns Data**: Shifts MP3's internal timestamps so they start exactly at 120s

### Handling Codec Changes

**Same Codec** (FLAC → FLAC):
- No `changeType()` needed
- Decoder stays initialized
- **Perfect gapless** (0ms gap guaranteed)

**Different Codec** (FLAC → MP3):
- Call `sourceBuffer.changeType('audio/mp4; codecs="mp3"')`
- Decoder reinitializes for new codec
- Timeline position preserved (still at 120s)
- **Near-perfect gapless** (possible 10-50ms gap during decoder switch, but no clicks if padding trimmed correctly)

### Visual Example: Three-Track Playlist

```
SourceBuffer Timeline:
┌─────────────────┬─────────────────┬─────────────────┐
│  Track 1 (FLAC) │  Track 2 (MP3)  │  Track 3 (AAC)  │
│  0s - 120s      │  120s - 360s    │  360s - 600s    │
│  No padding     │  Delay: 0.025s  │  Delay: 0.048s  │
│                 │  Padding:0.012s │  Padding:0.010s │
└─────────────────┴─────────────────┴─────────────────┘
                  ▲                 ▲
           changeType() to MP3  changeType() to AAC

Audio Element: <audio currentTime=0...600s (continuous)>
                      ▲
                      User hears seamless audio from 0-600s

Web Audio API: MediaElementSourceNode → GainNode → Destination
                      ▲
                      Effects/visualization work on continuous stream
```

### Code Example: Multi-Track Appending

```typescript
class MSEAudioLoader {
  private audioElement: HTMLAudioElement;
  private mediaSource: MediaSource;
  private sourceBuffer: SourceBuffer;
  private currentTimelinePosition: number = 0;
  private currentCodec: string | null = null;

  async appendTrack(track: TrackInfo) {
    // 1. Handle codec change
    const trackCodec = this.getCodecMimeType(track.codec);
    if (this.currentCodec !== trackCodec) {
      this.sourceBuffer.changeType(trackCodec);
      this.currentCodec = trackCodec;
      console.info(`🔄 Switched codec to ${track.codec}`);
    }

    // 2. Calculate trim durations from gapless metadata
    const delayDuration = track.gaplessInfo.encoderDelaySamples / track.gaplessInfo.sampleRate;
    const paddingDuration = track.gaplessInfo.paddingSamples / track.gaplessInfo.sampleRate;
    const realDuration = track.duration - delayDuration - paddingDuration;

    // 3. Configure append window for padding removal
    this.sourceBuffer.appendWindowStart = this.currentTimelinePosition;
    this.sourceBuffer.appendWindowEnd = this.currentTimelinePosition + realDuration;
    this.sourceBuffer.timestampOffset = this.currentTimelinePosition - delayDuration;

    console.info(`📍 Track ${track.title} positioned at ${this.currentTimelinePosition}s`);
    console.info(`   Duration: ${realDuration}s (trimmed ${delayDuration + paddingDuration}s padding)`);

    // 4. Fetch and append audio data progressively
    const response = await fetch(track.url);
    const reader = response.body.getReader();

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      await this.appendChunk(value);
    }

    // 5. Update timeline position for next track
    this.currentTimelinePosition += realDuration;
    console.info(`✅ Track appended, next track starts at ${this.currentTimelinePosition}s`);
  }

  private async appendChunk(chunk: Uint8Array) {
    if (this.sourceBuffer.updating) {
      await new Promise(resolve => {
        this.sourceBuffer.addEventListener('updateend', resolve, { once: true });
      });
    }

    this.sourceBuffer.appendBuffer(chunk);

    return new Promise(resolve => {
      this.sourceBuffer.addEventListener('updateend', resolve, { once: true });
    });
  }

  private getCodecMimeType(codec: string): string {
    switch (codec) {
      case 'flac': return 'audio/mp4; codecs="flac"';
      case 'mp3': return 'audio/mp4; codecs="mp3"';
      case 'aac': return 'audio/mp4; codecs="mp4a.40.2"';
      default: throw new Error(`Unsupported codec: ${codec}`);
    }
  }
}
```

### Seeking Across Track Boundaries

One of the benefits of the single SourceBuffer approach:

```typescript
// User seeks from Track 2 (200s) to Track 3 (400s)
audioElement.currentTime = 400;

// No special handling needed! The browser:
// 1. Jumps to 400s in the SourceBuffer
// 2. Finds Track 3's audio data
// 3. Resumes playback seamlessly

// Works because all tracks are in ONE continuous buffer
```

## Key MSE APIs

### 1. MediaSource & SourceBuffer Basics

```javascript
const audio = new Audio();
const mediaSource = new MediaSource();
audio.src = URL.createObjectURL(mediaSource);

mediaSource.addEventListener('sourceopen', () => {
  const sourceBuffer = mediaSource.addSourceBuffer('audio/mp4; codecs="flac"');

  // Progressive fetching and appending
  const response = await fetch(trackUrl);
  const reader = response.body.getReader();

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    await waitForSourceBufferReady(sourceBuffer);
    sourceBuffer.appendBuffer(value);
  }
});

audio.play();
```

### 2. SourceBuffer Modes

**Segments Mode (default)**:
- Segments can be appended in any order
- Playback order determined by embedded timestamps
- Requires manual `timestampOffset` management

**Sequence Mode**:
- Segments play in the order they were appended
- Browser automatically generates timestamps
- Simpler for sequential track playback

```javascript
sourceBuffer.mode = 'sequence';
```

### 3. `changeType()` - Codec Switching

Enables changing codec mid-stream without recreating MediaSource:

```javascript
// Start with FLAC
const sourceBuffer = mediaSource.addSourceBuffer('audio/mp4; codecs="flac"');
// ... append Track 1 (FLAC) data

// Switch to MP3 for Track 2
sourceBuffer.changeType('audio/mp4; codecs="mp3"');
// ... append Track 2 (MP3) data

// Switch to AAC for Track 3
sourceBuffer.changeType('audio/mp4; codecs="mp4a.40.2"');
// ... append Track 3 (AAC) data
```

**Browser Support**: Chrome 70+, Firefox 88+, Safari 14.5+, Edge 79+

**Key Behavior**:
- Preserves continuous buffer timeline
- Resets decoder for new codec
- Designed for DASH adaptive bitrate streaming
- **Gapless within same codec guaranteed**, small gaps possible at codec boundaries

### 4. Append Windows - Padding Removal

Trim unwanted audio frames (encoder padding) at segment boundaries:

```javascript
// Trim first 1024 samples (encoder delay) and last 500 samples (padding)
const delayDuration = 1024 / 44100;  // 0.0232s
const paddingDuration = 500 / 44100; // 0.0113s
const audioDuration = totalDuration - delayDuration - paddingDuration;

sourceBuffer.appendWindowStart = currentTime;
sourceBuffer.appendWindowEnd = currentTime + audioDuration;
sourceBuffer.timestampOffset = currentTime - delayDuration;

// Append segment - padding automatically trimmed by browser
sourceBuffer.appendBuffer(segment);
```

**How it works**:
- `appendWindowStart`/`End`: Defines the time range to accept
- Frames outside this window are discarded
- `timestampOffset`: Shifts embedded timestamps to position segment correctly
- Combine these to trim encoder padding at sample-accurate precision

### 5. Progressive Streaming with fetch()

Unlike HLS, we can stream regular files progressively:

```javascript
const response = await fetch(audioUrl);
const reader = response.body.getReader();

// Start playback as soon as first chunk arrives
let isFirstChunk = true;

while (true) {
  const { done, value } = await reader.read();
  if (done) break;

  if (sourceBuffer.updating) {
    await new Promise(resolve =>
      sourceBuffer.addEventListener('updateend', resolve, { once: true })
    );
  }

  sourceBuffer.appendBuffer(value);

  // Start playing after first chunk
  if (isFirstChunk) {
    audioElement.play(); // Instant playback!
    isFirstChunk = false;
  }
}
```

### 6. MIME Type Requirements for SourceBuffer

**MSE is extremely picky about MIME types!** The codec string in `addSourceBuffer()` must exactly match the codec in the file.

#### Why This Matters

```typescript
// WRONG - Generic container type won't work
mediaSource.addSourceBuffer('audio/mp4');  // ❌ Fails!

// RIGHT - Must specify exact codec
mediaSource.addSourceBuffer('audio/mp4; codecs="flac"');   // ✅ Works
mediaSource.addSourceBuffer('audio/mp4; codecs="mp3"');    // ✅ Works
mediaSource.addSourceBuffer('audio/mp4; codecs="mp4a.40.2"'); // ✅ AAC-LC
```

#### Coral's Current Architecture

**Backend Remuxing** (src/Coral.Encoders/Remux/FFmpeg.cs):
- Remuxes all formats (FLAC/MP3/AAC) to fragmented MP4 containers
- Uses `-hls_flags single_file` to create single `.m4s` file
- Detects source codec from file extension

**Current Issue:**
- Backend knows the codec but doesn't communicate it to frontend
- Frontend receives `OutputFormat.Remux` but not the specific codec

**Solution: Add Codec Info to API Response**

1. **Update `TranscodeInfoDto`** (src/Coral.Dto/Models/TranscodeInfoDto.cs):
```csharp
public record TranscodeInfoDto
{
    public required Guid JobId { get; set; }
    public required OutputFormat Format { get; set; }
    public required int Bitrate { get; set; }
    public string? Codec { get; set; }  // NEW: "flac", "mp3", "aac"
}
```

2. **Update `LibraryController.StreamTrack`** (src/Coral.Api/Controllers/LibraryController.cs):
```csharp
[HttpGet]
[Route("tracks/{trackId}/stream")]
public async Task<ActionResult<StreamDto>> StreamTrack(Guid trackId)
{
    var dbTrack = await _libraryService.GetTrack(trackId);
    if (dbTrack == null) return NotFound();

    // Detect codec from file extension
    var sourceCodec = Path.GetExtension(dbTrack.AudioFile.FilePath)
        .TrimStart('.').ToLowerInvariant() switch
    {
        "flac" => "flac",
        "m4a" or "aac" => "aac",
        "mp3" => "mp3",
        _ => null
    };

    var job = await _transcoderService.CreateJob(OutputFormat.Remux, opt =>
    {
        opt.SourceTrack = dbTrack;
        opt.Bitrate = 0;
        opt.RequestType = TranscodeRequestType.HLS;
    });

    return new StreamDto()
    {
        Link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/hls/{job.Id}/playlist.m4s",
        TranscodeInfo = new TranscodeInfoDto()
        {
            JobId = job.Id,
            Bitrate = 0,
            Format = OutputFormat.Remux,
            Codec = sourceCodec  // NEW: Pass codec to frontend
        }
    };
}
```

3. **Frontend Uses Codec Info**:
```typescript
// Fetch stream info
const response = await fetch(`/api/library/tracks/${trackId}/stream`);
const streamData = await response.json();

// Map codec to MSE MIME type
const codecMimeType = {
  'flac': 'audio/mp4; codecs="flac"',
  'mp3': 'audio/mp4; codecs="mp3"',
  'aac': 'audio/mp4; codecs="mp4a.40.2"'
}[streamData.transcodeInfo.codec];

// Validate browser support
if (!MediaSource.isTypeSupported(codecMimeType)) {
  throw new Error(`Browser doesn't support ${codecMimeType}`);
}

// Create SourceBuffer with correct MIME type
const sourceBuffer = mediaSource.addSourceBuffer(codecMimeType);

// Fetch and stream the .m4s file
const audioResponse = await fetch(streamData.link);
// ... stream to SourceBuffer
```

#### Codec MIME Type Reference

| Source File | Container After Remux | MSE MIME Type |
|------------|----------------------|---------------|
| `track.flac` | fMP4 with FLAC | `audio/mp4; codecs="flac"` |
| `track.mp3` | fMP4 with MP3 | `audio/mp4; codecs="mp3"` |
| `track.m4a` | fMP4 with AAC-LC | `audio/mp4; codecs="mp4a.40.2"` |

**Note on HLS Playlist:**
- Current backend creates both `playlist.m3u8` and `playlist.m4s`
- Frontend can **skip the M3U8** and fetch `/hls/{jobId}/playlist.m4s` directly
- The M3U8 contains byte ranges for 10-second segments, but MSE can stream the whole file progressively

### 7. Seeking with Range Requests

For unbuffered seeks, use HTTP Range headers:

```javascript
audioElement.addEventListener('seeking', async () => {
  const seekTime = audioElement.currentTime;

  // Check if position is buffered
  const isBuffered = isTimeBuffered(sourceBuffer.buffered, seekTime);

  if (!isBuffered) {
    // Clear old buffer
    const bufferedEnd = sourceBuffer.buffered.length > 0
      ? sourceBuffer.buffered.end(0)
      : 0;
    if (bufferedEnd > 0) {
      sourceBuffer.remove(0, bufferedEnd);
      await waitForUpdateEnd(sourceBuffer);
    }

    // Estimate byte offset (for CBR) or use seek table
    const byteOffset = estimateByteOffset(seekTime, track);

    // Fetch from new position
    const response = await fetch(trackUrl, {
      headers: { Range: `bytes=${byteOffset}-` }
    });

    // Append from new position
    streamToSourceBuffer(response, sourceBuffer, seekTime);
  }
});
```

**Note**: For initial implementation, native seeking works fine for buffered content. Range request seeking is an optimization for later.

## Gapless Metadata Requirements

### What We Need

For MP3 and AAC files, we need to extract:

1. **`encoder_delay_samples`**: Samples added at start by encoder
2. **`padding_samples`**: Samples added at end by encoder
3. **`sample_rate`**: Audio sample rate (e.g., 44100 Hz)
4. **`codec`**: Codec identifier (e.g., "mp3", "aac", "flac")

### Where to Find It

**MP3 Files**:
- LAME encoder header (Xing/Info frame)
- Located in first MP3 frame
- Contains `encoder_delay` and `padding` fields

**M4A/AAC Files**:
- MP4 `edts` atom (edit list)
- Specifies media start time and duration
- Indicates samples to skip at start/end

**FLAC Files**:
- No encoder padding (lossless PCM compression)
- No trim metadata needed

### Extracting Metadata with FFprobe

**Great news**: FFprobe (part of FFmpeg) can automatically extract gapless metadata without manual binary parsing!

**Example FFprobe Output**:

```
# M4A/AAC File
Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'track.m4a':
  Metadata:
    encoder         : qaac 2.67, CoreAudioToolbox 7.9.8.3, AAC-LC Encoder
    iTunSMPB        :  00000000 00000840 000001A5 00000000131CDA1B ...
  Duration: 02:01:11.21, start: 0.047891, bitrate: 192 kb/s
  Stream #0:0[0x1](und): Audio: aac (LC), 44100 Hz, stereo, fltp, 191 kb/s

# MP3 File
Input #0, mp3, from 'track.mp3':
  Metadata:
    encoder         : LAME3.99r
  Duration: 01:55:59.36, start: 0.025057, bitrate: 320 kb/s
  Stream #0:0: Audio: mp3, 44100 Hz, stereo, fltp, 320 kb/s
```

**Decoding the iTunSMPB Tag** (M4A/AAC):
```
iTunSMPB: 00000000 00000840 000001A5 00000000131CDA1B ...
          |        |        |        |
          |        |        |        └─ Total samples (hex)
          |        |        └─ Padding samples: 0x1A5 = 421 samples
          |        └─ Encoder delay: 0x840 = 2112 samples
          └─ Version (ignored)
```

**Using start_time** (MP3/AAC):
```
start: 0.047891s
0.047891 × 44100 Hz = 2112 samples (encoder delay)
```

**FFprobe Command**:
```bash
ffprobe -v quiet -print_format json -show_format -show_streams "track.m4a"
```

**Parsing in C#**:
```csharp
// Example parsing logic
var ffprobeResult = await Cli.Wrap("ffprobe")
    .WithArguments(args => args
        .Add("-v").Add("quiet")
        .Add("-print_format").Add("json")
        .Add("-show_format")
        .Add("-show_streams")
        .Add(filePath))
    .ExecuteBufferedAsync();

var json = JsonSerializer.Deserialize<FFprobeOutput>(ffprobeResult.StandardOutput);

int encoderDelay = 0;
int padding = 0;
int sampleRate = json.Streams[0].SampleRate;

// Method 1: iTunes gapless tag (M4A/AAC)
if (json.Format.Tags?.TryGetValue("iTunSMPB", out var iTunSMPB) == true)
{
    var parts = iTunSMPB.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length >= 3)
    {
        encoderDelay = Convert.ToInt32(parts[1], 16);  // Hex to int
        padding = Convert.ToInt32(parts[2], 16);
    }
}

// Method 2: start_time field (MP3, or M4A without iTunes tag)
if (encoderDelay == 0 && json.Format.StartTime.HasValue)
{
    encoderDelay = (int)(json.Format.StartTime.Value * sampleRate);
}

// Note: For end padding, might need to parse LAME header or use heuristics
// Most MP3 encoders add ~576-1152 samples of padding
```

**Key Advantages**:
- ✅ No manual binary parsing needed
- ✅ Handles both iTunSMPB tag and LAME headers
- ✅ Already part of FFmpeg (which we use for remuxing)
- ✅ Consistent, well-tested implementation

### Example Metadata Structure

Based on Spotify's approach:

```json
{
  "encoder_delay_samples": 1024,
  "padding_samples": 500,
  "sample_rate": 44100,
  "codec": "aac"
}
```

Durations calculated as:
```javascript
const delayDuration = encoder_delay_samples / sample_rate;
const paddingDuration = padding_samples / sample_rate;
const realDuration = totalDuration - delayDuration - paddingDuration;
```

## Implementation Plan

### Phase 1: MSE with FLAC (Proof of Concept)

**Goal**: Implement MSE infrastructure and validate gapless playback with FLAC files

**Why FLAC first?**
- ✅ No encoder padding - simplest case
- ✅ Tests MSE infrastructure without metadata complexity
- ✅ Validates Web Audio API integration
- ✅ Proves the gapless concept

**Frontend Tasks**:
1. Create `MSEAudioLoader` class:
   - Manage MediaSource lifecycle (sourceopen → append → endOfStream)
   - Manage SourceBuffer (create, append, updateend events)
   - **Progressive file streaming** with `fetch()` and ReadableStream
   - Simple timeline management (no padding concerns for FLAC)
   - Queue multiple tracks and append sequentially

2. Update `WebAudioPlayer` class:
   - Replace AudioBufferSourceNode with audio element
   - Create MediaElementSourceNode for Web Audio API
   - Connect to gain node for volume control
   - Simplified playback controls (audio element handles buffering)
   - Track changes coordinate with loader

3. Test with FLAC-only playlists

**Backend Requirements**:
1. **Add Codec Info to API Response**:
   - Update `TranscodeInfoDto` to include `Codec` field
   - Update `LibraryController.StreamTrack` to detect and return source codec
   - This is **required** for MSE to work - frontend needs exact codec MIME type

2. **File Serving** (current approach works):
   - Continue using fMP4 remux with `-hls_flags single_file`
   - Serves `/hls/{jobId}/playlist.m4s` directly (skip M3U8 parsing)
   - ~170-200ms remuxing time for typical files (acceptable)

**Success Criteria**:
- ✅ Gapless playback between FLAC tracks
- ✅ No clicks or gaps
- ✅ Instant startup (first chunk plays immediately)
- ✅ Progressive loading (don't wait for entire track)

### Phase 2: Gapless Metadata Extraction

**Goal**: Extract and store encoder padding metadata for MP3/AAC files

**Backend Tasks**:

1. **Implement Metadata Extraction with FFprobe** (C#):
   - Create FFprobe wrapper service
   - Parse JSON output to extract:
     - `iTunSMPB` tag (M4A/AAC files)
     - `start_time` field (MP3 files, fallback for AAC)
     - Sample rate from stream info
   - Handle missing metadata gracefully (assume 0 padding for FLAC)
   - Note: FFmpeg already installed for remuxing, no new dependencies!

2. **Database Schema**:
   ```sql
   ALTER TABLE AudioFiles ADD COLUMN EncoderDelaySamples INT;
   ALTER TABLE AudioFiles ADD COLUMN PaddingSamples INT;
   ALTER TABLE AudioFiles ADD COLUMN SampleRate INT;
   ALTER TABLE AudioFiles ADD COLUMN Codec VARCHAR(10);
   ```

3. **Update IndexerService**:
   - Call metadata parsers during file indexing
   - Store gapless info in database

4. **Update API DTOs**:
   ```csharp
   public class GaplessInfoDto
   {
       public int EncoderDelaySamples { get; set; }
       public int PaddingSamples { get; set; }
       public int SampleRate { get; set; }
       public string Codec { get; set; }
   }

   public class TrackDto
   {
       // ... existing fields
       public GaplessInfoDto? GaplessInfo { get; set; }
   }
   ```

5. **Include in stream endpoint**:
   - Return gapless metadata with stream URL
   - Frontend uses this to configure append windows

**Success Criteria**:
- ✅ Gapless metadata extracted during indexing
- ✅ Metadata available in API responses
- ✅ Covers MP3, M4A/AAC, and FLAC files

### Phase 3: Mixed Codec Gapless Playback

**Goal**: Support gapless transitions across codec changes (AAC → MP3 → FLAC)

**Frontend Tasks**:

1. **Update `MSEAudioLoader`**:
   - Detect codec changes between tracks
   - Call `changeType()` when codec switches
   - Apply `appendWindowStart/End` based on gapless metadata
   - Adjust `timestampOffset` for encoder delay
   - Handle edge cases (codec not supported, trim calculation errors)

2. **Enhanced Timeline Management**:
   ```typescript
   class MSEAudioLoader {
     private currentTime: number = 0;
     private currentCodec: string | null = null;

     async appendTrack(track: TrackWithGapless) {
       // Codec switch if needed
       if (this.currentCodec !== track.gaplessInfo.codec) {
         this.sourceBuffer.changeType(getCodecMimeType(track.gaplessInfo.codec));
         this.currentCodec = track.gaplessInfo.codec;
       }

       // Calculate trim durations
       const delayDuration = track.gaplessInfo.encoderDelaySamples / track.gaplessInfo.sampleRate;
       const paddingDuration = track.gaplessInfo.paddingSamples / track.gaplessInfo.sampleRate;
       const realDuration = track.duration - delayDuration - paddingDuration;

       // Set append window to trim padding
       this.sourceBuffer.appendWindowStart = this.currentTime;
       this.sourceBuffer.appendWindowEnd = this.currentTime + realDuration;
       this.sourceBuffer.timestampOffset = this.currentTime - delayDuration;

       // Fetch and append progressively
       const response = await fetch(track.url);
       const reader = response.body.getReader();

       while (true) {
         const { done, value } = await reader.read();
         if (done) break;
         await this.appendChunk(value);
       }

       this.currentTime += realDuration;
     }
   }
   ```

3. **Test mixed codec playlists**:
   - FLAC → MP3 transition
   - MP3 → AAC transition
   - AAC → FLAC transition

**Success Criteria**:
- ✅ No clicks at track boundaries (padding properly trimmed)
- ✅ Same-codec transitions: perfect gapless
- ✅ Codec-change transitions: minimal/no gap
- ✅ Timeline remains continuous

## Technical References

### Documentation
- [MDN: SourceBuffer.changeType()](https://developer.mozilla.org/en-US/docs/Web/API/SourceBuffer/changeType)
- [MDN: SourceBuffer.mode](https://developer.mozilla.org/en-US/docs/Web/API/SourceBuffer/mode)
- [MDN: SourceBuffer.timestampOffset](https://developer.mozilla.org/en-US/docs/Web/API/SourceBuffer/timestampOffset)
- [W3C Media Source Extensions Spec](https://www.w3.org/TR/media-source-2/)

### Articles & Examples
- [web.dev: MSE for Audio - Seamless Playback](https://web.dev/articles/mse-seamless-playback) - Uses regular MP3 file, not HLS!
- [Chrome Developers: MSE SourceBuffer Sequence Mode](https://developer.chrome.com/blog/mse-sourcebuffer)
- [Chrome Sample: Codec Switching with changeType()](https://googlechrome.github.io/samples/media/sourcebuffer-changetype.html)

### Real-World Examples
- **Spotify Web Player**: Streams AAC files directly via MSE (encrypted), no HLS
- **YouTube Music**: Uses MSE with regular audio files
- **Apple Music Web**: Uses MSE with HLS for DRM, but MSE works fine without HLS

### Key Insights

1. **HLS is optional for MSE**: Spotify and modern examples use direct file streaming
2. **`changeType()` is designed for adaptive bitrate streaming**, not specifically for gapless playback, but works for our use case
3. **Sequence mode simplifies timeline management** by auto-generating timestamps
4. **Append windows operate at the decoded sample level** - perfect for trim precision
5. **Same-codec transitions should be perfectly gapless**, codec changes may have small gaps
6. **MSE was originally for video (DASH)** but works excellently for audio with proper configuration
7. **Progressive streaming is instant**: No need to wait for full file, playback starts with first chunk

## Expected Results

### With Gapless Metadata

**Same Codec Transitions** (e.g., FLAC → FLAC):
- ✅ Perfect gapless (0ms gap)
- ✅ No clicks
- ✅ Sample-accurate

**Codec Changes** (e.g., AAC → MP3):
- ✅ No clicks (proper padding trim)
- ⚠️ Possible small gap (10-50ms) due to decoder reinitialization
- ✅ Still significantly better than current approach

### Without Gapless Metadata (Phase 1)

**FLAC Files**:
- ✅ Perfect gapless (FLAC has no padding)
- ✅ Validates MSE infrastructure

**MP3/AAC Files**:
- ⚠️ Clicks at boundaries (padding not trimmed)
- ✅ But proves MSE works, ready for Phase 2

## Alternative Approaches Considered (and Why They Don't Work)

### ❌ Backend Transcoding to Uniform Codec
- User requirement: Preserve source codecs (no transcoding)

### ❌ Backend Transcoding to FLAC
- Excessive bandwidth (~800-1200 kbps vs 128-320 kbps)
- Slower transcoding than remux

### ❌ Dual Audio Element Crossfading
- Already tried and failed due to timing inconsistencies
- Both DOM-level and Web Audio API gain node crossfading had issues

### ❌ Single SourceBuffer without `changeType()`
- Cannot handle codec changes in playlists
- Would require backend to transcode

### ❌ Using HLS.js for Multi-Track Playback
- hls.js `loadSource()` recreates MediaSource on source change
- Cannot maintain continuous SourceBuffer across tracks
- Would require 3-4 weeks of architectural modifications vs 1-2 days for custom implementation

## Next Steps

1. ✅ Document research (this file)
2. 🔄 Implement Phase 1: MSE with FLAC (direct file streaming)
3. ⏳ Implement Phase 2: Gapless metadata extraction
4. ⏳ Implement Phase 3: Mixed codec support

---

*Last updated: 2025-10-12 (Added MIME type requirements and codec detection)*
