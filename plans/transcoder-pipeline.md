# Transcoder Pipeline Architecture Plan

## Problem Statement

Browsers have strict requirements about codec/container combinations in MSE:
- **Firefox**: Supports MP3 in fMP4 (`audio/mp4; codecs="mp3"`)
- **Chrome**: Requires transmuxing for MP3 (TS → fMP4 or raw MPEG)
- **All browsers**: Different support for FLAC, AAC, ALAC, etc.

Current issue: MP3 gapless playback requires different approaches per browser.

## User System & Device Preferences

**Prerequisite:** Before implementing the capability-based transcoding pipeline, we need a user authentication system to support per-device playback preferences.

**See [authentication-system.md](./authentication-system.md) for complete authentication architecture.**

### Quick Summary

- **Authentication:** Username/password with JWT (1-day access tokens, non-expiring refresh tokens)
- **Auth Exemptions:** Single-user mode and LAN/localhost access don't require login
- **Per-Device Preferences:** Each device can configure quality (Direct Play, Direct Stream, Lossless/Lossy Transcode), bandwidth limits, and playback settings
- **Automatic Token Refresh:** Frontend refreshes tokens transparently, users never see "session expired" errors

### Integration with Transcoding Pipeline

Stream endpoint checks user preferences:

```csharp
[Authorize]
[HttpGet("tracks/{trackId}/stream")]
public async Task<IActionResult> StreamTrack(
    int trackId,
    [FromHeader(Name = "X-Device-Id")] string deviceId)
{
    var userId = GetCurrentUserId();
    var device = await _deviceService.GetDevice(userId, deviceId);
    var preferences = device.Preferences;
    var capabilities = await _capabilityService.GetCapabilities(deviceId);

    var outputFormat = _capabilityService.DetermineOptimalFormat(
        track.Codec,
        track.Container,
        capabilities,
        preferences.Quality,
        preferences.GetEffectiveBitrateLimit(device.Type)
    );

    // Stream or transcode based on outputFormat.Method
}
```

**Fallback behavior for Direct Play:**
1. Try direct play (stream original file if codec+container supported)
2. Fallback to direct stream (remux to compatible container if codec supported)
3. Error if codec not supported in any container (unless user allows lossy transcode)

## Input Formats We Support

### Lossless Formats
- FLAC
- ALAC (Apple Lossless)
- WAV (uncompressed PCM)
- AIFF (uncompressed PCM)
- APE (Monkey's Audio)
- WavPack
- TTA (True Audio)

### Lossy Formats
- MP3
- AAC (M4A)
- Opus
- Ogg Vorbis

## Browser MSE Support Matrix (Research Needed)

### Chrome
- ✅ FLAC in fMP4: `audio/mp4; codecs="flac"`
- ✅ AAC in fMP4: `audio/mp4; codecs="mp4a.40.2"`
- ✅ Opus in WebM: `audio/webm; codecs="opus"`
- ✅ Vorbis in WebM: `audio/webm; codecs="vorbis"`
- ✅ MP3 as raw MPEG: `audio/mpeg` (requires demuxing)
- ❌ MP3 in fMP4: Not reliable for gapless
- ❌ ALAC, AIFF, WAV: Not supported directly

### Firefox
- ✅ FLAC in fMP4: `audio/mp4; codecs="flac"`
- ✅ MP3 in fMP4: `audio/mp4; codecs="mp3"`
- ✅ AAC in fMP4: `audio/mp4; codecs="mp4a.40.2"`
- ✅ Opus in WebM: `audio/webm; codecs="opus"`
- ✅ Vorbis in WebM: `audio/webm; codecs="vorbis"`
- ❌ ALAC, AIFF, WAV: Not supported

### Safari (Future)
- ✅ FLAC in fMP4: `audio/mp4; codecs="flac"`
- ✅ ALAC in fMP4: `audio/mp4; codecs="alac"`
- ✅ AAC in fMP4: `audio/mp4; codecs="mp4a.40.2"`
- ✅ MP3 as raw MPEG: `audio/mpeg`
- ❌ Opus, Vorbis: Limited WebM support

### Android/iOS (Future)
**Note:** Mobile platforms use native APIs, not MSE:
- **iOS**: AVQueuePlayer (native Apple API)
- **Android**: Media3 (Jetpack Media library)

Both have broader native format support than MSE. Capability detection still needed but different API surface.

## Proposed Architecture

### 1. Frontend: Capability Detection

**New file: `lib/player/capabilities.ts`**

```typescript
interface PlayerCapabilities {
  supportedMimeTypes: string[];
  platform: 'web' | 'android' | 'ios';
  browser?: 'chrome' | 'firefox' | 'safari' | 'edge';
  preferredFormats: {
    mp3?: 'fmp4' | 'mpeg' | 'transcode';
    flac?: 'fmp4' | 'transcode';
    aac?: 'fmp4' | 'transcode';
    opus?: 'webm' | 'transcode';
    // etc.
  };
}

function detectCapabilities(): PlayerCapabilities {
  // Test MediaSource.isTypeSupported() for various MIME types
  // Return what this player supports
}
```

**On player initialization:**
- Detect all supported MIME types
- Send capabilities to backend once (cache in session/local storage)

### 2. Backend: Capability-Based Transcoding

**New endpoint: `POST /api/player/capabilities`**
- Frontend sends capabilities
- Backend stores in session or returns a capabilities token

**Modified endpoint: `GET /api/library/tracks/{trackId}/stream`**
- Read player capabilities from session/header
- Decide optimal output format based on:
  1. Source format
  2. Player capabilities
  3. Performance preferences (remux > transcode)

**New service: `ICapabilityService`**
```csharp
public interface ICapabilityService
{
    OutputFormat DetermineOptimalFormat(
        string sourceCodec,
        PlayerCapabilities capabilities);

    bool RequiresTranscoding(
        string sourceCodec,
        OutputFormat targetFormat);

    bool IsLosslessFormat(string codec);

    // Returns null if lossy format can't be served (refuses lossy → lossy)
    OutputFormat? GetSupportedFormatOrNull(
        string sourceCodec,
        PlayerCapabilities capabilities);
}
```

### 3. Transcoding Decision Matrix

**CRITICAL RULE: Warn before lossy → lossy transcodes!**
Transcoding from one lossy format to another (e.g., Opus → AAC on Safari) compounds generation loss and degrades audio quality. If a lossy format isn't supported, we should:
1. **Warn the user** with clear explanation of quality loss
2. **Allow user to accept** the transcode after warning
3. **Remember preference** per-format or globally
4. **For lossless formats**: Always transcode to FLAC (no warning needed)

| Source Format | Chrome Target | Firefox Target | Safari Target | Notes |
|--------------|---------------|----------------|---------------|-------|
| **Lossy Formats** |
| MP3 | Raw MPEG (remux TS) | fMP4 (remux) | Raw MPEG (remux TS) | Gapless varies |
| AAC/M4A | fMP4 (remux) | fMP4 (remux) | fMP4 (remux) | Universal |
| Opus | WebM (remux) | WebM (remux) | AAC (warn user) | Lossy→lossy on Safari |
| Ogg Vorbis | WebM (remux) | WebM (remux) | AAC (warn user) | Lossy→lossy on Safari |
| **Lossless Formats** |
| FLAC | fMP4 (remux) | fMP4 (remux) | fMP4 (remux) | Universal support |
| ALAC | FLAC (transcode) | FLAC (transcode) | fMP4 (remux) | Safari native |
| WAV/AIFF (16/24-bit) | FLAC (transcode) | FLAC (transcode) | FLAC (transcode) | Integer depths OK |
| WAV/AIFF (32-bit float) | **ERROR** | **ERROR** | **ERROR** | FLAC can't preserve float |
| APE/WavPack/TTA | FLAC (transcode) | FLAC (transcode) | FLAC (transcode) | Transcode to FLAC |

**Decision Priority:**
1. **Remux** if source codec supported in target container (fastest)
2. **Transcode lossless → FLAC** if codec not supported (no warning)
3. **Warn + Transcode lossy → AAC** if lossy codec not supported (user consent required)

### 4. Frontend: Simplified Player Logic

**No more browser detection in player code!**

```typescript
// Player just requests stream and plays whatever format backend sends
const streamInfo = await fetch(`/api/library/tracks/${trackId}/stream`, {
  headers: {
    'X-Player-Capabilities': capabilitiesToken
  }
});

// Backend already sent appropriate format based on capabilities
// Just create SourceBuffer with returned MIME type and play
```

### 5. Implementation Steps (Priority Order)

#### Phase 0: User System & Device Management (Prerequisite)

**See [authentication-system.md](./authentication-system.md) for detailed implementation checklist and architecture.**

High-level tasks:
- [ ] Backend: Implement user authentication system (JWT with refresh tokens)
- [ ] Backend: Implement device registration and preference management
- [ ] Backend: Update existing endpoints to require authentication
- [ ] Frontend: Implement login flow with auth exemption check
- [ ] Frontend: Implement automatic token refresh
- [ ] Frontend: Create device registration and preferences UI

#### Phase 1: Capability Detection (Frontend)
- [ ] Create `capabilities.ts` with detection logic
- [ ] Test `MediaSource.isTypeSupported()` for all relevant MIME types
- [ ] Store capabilities in session storage
- [ ] Create `POST /api/player/capabilities` endpoint

#### Phase 2: Backend Decision Logic
- [ ] Create `PlayerCapabilities` DTO
- [ ] Create `ICapabilityService` interface
- [ ] Implement capability-based format selection
- [ ] Update `TranscoderService` to accept target format hints
- [ ] Modify stream endpoint to use capabilities

#### Phase 3: Format-Specific Handlers
- [ ] Ensure FFmpeg remux handles all cases correctly
- [ ] Add conditional container logic (fMP4 vs TS vs WebM)
- [ ] Test each source format → target format combination

#### Phase 4: Frontend Simplification
- [ ] Remove browser detection from player
- [ ] Remove manual demuxing logic (or make it codec-specific)
- [ ] Trust backend to send correct format
- [ ] Handle MIME type from API response

#### Phase 5: Mobile Support (Future)
- [ ] Research Android/iOS MSE capabilities
- [ ] Add platform-specific capability detection
- [ ] Test on real devices

### 6. Migration Path (Tomorrow)

**Immediate steps:**
1. Revert to fMP4 for MP3 (accept gaps in Chrome for now)
2. Start with capability detection implementation
3. Build backend decision logic
4. Re-introduce gapless support once pipeline is complete

**Why revert first?**
- Gets playback working in both browsers immediately
- Allows us to build proper architecture without time pressure
- Can re-add gapless as optimization later

### 7. Open Questions for Tomorrow

1. Should we cache capabilities per-session or per-browser?
2. Do we need a fallback transcoding queue for slow conversions?
3. Should we support multiple quality options (bitrates)?
4. How do we handle format upgrades (e.g., new browser version supports FLAC)?
5. Do we need to detect HDR/Dolby support for future video features?
6. **32-bit float WAV handling:** Should we:
   - Return HTTP 415 error (cleanest, but blocks playback)
   - Dither to 24-bit FLAC with warning (technically lossy but preserves 99% quality)
   - Add user preference for "allow float→int conversion"?
7. Should we notify users if they have unsupported files in their library (e.g., scan for 32-bit float WAVs)?

### 8. Quality Warning System for Lossy Transcodes

#### Unsupported Lossy Formats - User Warning Flow
When a lossy format can't be remuxed (e.g., Opus on Safari, WMA anywhere):

**Backend Response (on first request):**
```
HTTP 409 Conflict (Quality Warning Required)
{
  "error": "QualityWarningRequired",
  "message": "Opus files cannot be played natively on Safari. Transcoding to AAC will result in quality loss.",
  "sourceCodec": "opus",
  "targetCodec": "aac",
  "warningType": "lossy_to_lossy",
  "acceptEndpoint": "/api/library/tracks/{trackId}/stream?acceptLossyTranscode=true"
}
```

**Frontend Dialog:**
```
⚠️ Audio Quality Warning

This Opus file cannot be played natively on Safari.

To play this track, we need to transcode it to AAC, which will cause additional quality loss (lossy → lossy conversion).

[ ] Remember my choice for Opus files
[ ] Remember my choice for all unsupported formats

[Cancel]  [Accept & Play]
```

**After User Accepts:**
- Frontend re-requests stream with `?acceptLossyTranscode=true`
- Backend transcodes Opus → AAC and serves
- Preference saved (per-format or global)

**Preference Storage:**
```typescript
interface TranscodePreferences {
  allowLossyTranscodes: boolean | 'ask';  // Global setting
  perFormatOverrides: {
    'opus': 'allow' | 'deny' | 'ask';
    'vorbis': 'allow' | 'deny' | 'ask';
  };
}
```

#### 32-bit Float WAV/AIFF
Special case: Lossless format that CANNOT be transcoded without loss (FLAC doesn't support float):

```
HTTP 415 Unsupported Media Type
{
  "error": "UnsupportedPrecision",
  "message": "32-bit floating-point audio cannot be transcoded to FLAC without precision loss.",
  "sourceCodec": "pcm_f32le",
  "bitDepth": 32,
  "suggestion": "Consider converting to 24-bit integer format for compatibility."
}
```

**Alternative:** Offer dithered 24-bit conversion with explicit user consent.

### 9. Testing Matrix

Need to test all combinations:

**Lossy Formats:**
- [ ] MP3 → Chrome (should remux TS)
- [ ] MP3 → Firefox (should remux fMP4)
- [ ] MP3 → Safari (should remux TS)
- [ ] AAC → All browsers (should remux fMP4)
- [ ] Opus → Chrome (should remux WebM)
- [ ] Opus → Firefox (should remux WebM)
- [ ] Opus → Safari (should ERROR)
- [ ] Vorbis → Chrome (should remux WebM)
- [ ] Vorbis → Firefox (should remux WebM)
- [ ] Vorbis → Safari (should ERROR)

**Lossless Formats:**
- [ ] FLAC → All browsers (should remux fMP4)
- [ ] ALAC → Chrome (should transcode to FLAC)
- [ ] ALAC → Firefox (should transcode to FLAC)
- [ ] ALAC → Safari (should remux fMP4)
- [ ] WAV 16-bit → All browsers (should transcode to FLAC)
- [ ] WAV 24-bit → All browsers (should transcode to FLAC)
- [ ] WAV 32-bit float → All browsers (should ERROR)
- [ ] AIFF → All browsers (should transcode to FLAC)

### 10. Lossless Format Catalog (Backend)

Backend must maintain a definitive list of lossless codecs:

```csharp
public static class AudioCodecs
{
    public static readonly HashSet<string> LosslessCodecs = new()
    {
        "flac",
        "alac",
        "wav", "pcm_s16le", "pcm_s24le", "pcm_s32le",
        "aiff", "pcm_s16be", "pcm_s24be", "pcm_s32be",
        "ape",      // Monkey's Audio
        "wv",       // WavPack
        "tta",      // True Audio
        "tak",      // Tom's lossless Audio Kompressor
        "mlp",      // MLP/TrueHD
        "dts"       // DTS-HD Master Audio (lossless core)
    };

    public static readonly HashSet<string> LossyCodecs = new()
    {
        "mp3",
        "aac", "mp4a",
        "opus",
        "vorbis"
    };

    // Special case: 32-bit float WAV/AIFF
    public static readonly HashSet<string> FloatingPointCodecs = new()
    {
        "pcm_f32le",  // 32-bit float little-endian
        "pcm_f32be",  // 32-bit float big-endian
        "pcm_f64le",  // 64-bit float little-endian
        "pcm_f64be"   // 64-bit float big-endian
    };

    public static bool IsLossless(string codec)
    {
        return LosslessCodecs.Contains(codec.ToLower())
            || FloatingPointCodecs.Contains(codec.ToLower());
    }

    public static bool IsFloatingPoint(string codec)
    {
        return FloatingPointCodecs.Contains(codec.ToLower());
    }
}
```

**Usage in transcoding decisions:**
```csharp
// Check if user needs to consent to lossy → lossy transcode
if (!IsLossless(sourceCodec) && !CanRemux(sourceCodec, targetFormat))
{
    // Check if user has already consented
    if (!request.AcceptLossyTranscode && !HasUserConsent(userId, sourceCodec))
    {
        return new QualityWarningRequired(
            sourceCodec: sourceCodec,
            targetCodec: "aac",
            warningType: "lossy_to_lossy"
        );
    }

    // User consented, proceed with transcode
    return TranscodeToAAC(sourceFile, capabilities);
}

// Special handling for 32-bit float WAV
if (IsFloatingPoint(sourceCodec))
{
    // FLAC doesn't support floating point - hard error (cannot preserve quality)
    return new UnsupportedPrecisionError(
        sourceCodec,
        bitDepth: 32,
        message: "32-bit floating-point audio cannot be transcoded to FLAC without precision loss."
    );
}
```

### 11. Success Criteria

- ✅ User authentication system with JWT tokens
- ✅ Per-device playback preferences (Direct Play, Direct Stream, Lossless/Lossy Transcode)
- ✅ Bandwidth limits configurable per device (with cellular override)
- ✅ All supported formats play in Chrome/Firefox/Safari
- ✅ MP3 gapless playback in Chrome (eventually)
- ✅ **Warn users before lossy → lossy transcoding**
- ✅ User consent required and remembered for quality-loss transcodes
- ✅ Lossless formats automatically transcode to FLAC when unsupported
- ✅ Clear error messages for truly unsupported formats (32-bit float)
- ✅ No browser-specific code in player
- ✅ Fast remuxing when possible
- ✅ Graceful user experience for edge cases
- ✅ Ready for mobile expansion

---

## Summary

**The core insight:** The backend should decide what to serve based on client capabilities AND user preferences, not the frontend deciding how to parse whatever it gets.

**Key principles:**
1. ✅ **Per-device preferences** (phone vs desktop have different needs)
2. ✅ **Direct Play with fallback** (try as-is, fallback to remux automatically)
3. ✅ **Always remux when possible** (fastest, preserves quality)
4. ✅ **Transcode lossless → FLAC** when unsupported (safe, no quality loss)
5. ⚠️ **Warn before lossy → lossy** (user consent required, remembers preference)
6. ❌ **Hard error for 32-bit float WAV** (cannot preserve without loss)

**Implementation order:**
1. **Phase 0:** Build user authentication & device management system (prerequisite) - see [authentication-system.md](./authentication-system.md)
2. **Phase 1:** Implement capability detection in frontend
3. **Phase 2:** Build decision matrix in backend (respects user preferences)
4. **Phase 3:** Add quality warning system for lossy transcodes
5. **Phase 4:** Test matrix of format combinations
