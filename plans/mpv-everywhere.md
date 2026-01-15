# Plan: libmpv Everywhere

## Goal
Build audio-only FFmpeg + libmpv from source for **all platforms**:
- **Mobile:** iOS, Android (replacing expo-audio)
- **Desktop:** Windows, macOS, Linux (for Electron app)

Benefits:
- True gapless playback (via mpv's native playlist handling)
- Better codec/format support (mpv supports virtually everything)
- Unified player backend across all platforms
- Full control over dependencies and binary size
- No reliance on third-party pre-built binaries

## Existing Building Blocks

| Component | Source | Status |
|-----------|--------|--------|
| Electron libmpv | Your codebase (`electron/mpv-*.mjs`) | ✅ Working |
| Android libmpv builds | [mpv-android](https://github.com/mpv-android/mpv-android) | Available |
| iOS libmpv builds | [karelrooted/libmpv](https://github.com/karelrooted/libmpv) | Available |
| React Native binding | [react-native-mpv](https://github.com/Dusk-Labs/react-native-mpv) | Android-only |
| PlayerBackend interface | Your codebase (`lib/player/player-backend.ts`) | ✅ Ready |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     React Components                        │
│                    (PlayerProvider, etc.)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   PlayerBackend Interface                   │
│          loadQueue, play, pause, skip, seekTo, etc.         │
└─────────────────────────────────────────────────────────────┘
          │                   │                    │
          ▼                   ▼                    ▼
┌─────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ MpvIpcProxy │    │  MpvNativeProxy  │    │ MSEWebAudioPlayer│
│  (Electron) │    │   (iOS/Android)  │    │     (Web)        │
└─────────────┘    └──────────────────┘    └─────────────────┘
       │                    │                       │
       ▼                    ▼                       ▼
   libmpv.dll         libmpv.dylib/so          Web Audio API
   (via koffi)      (via TurboModule)          + MSE
```

## Implementation Approach: Build from Scratch

We're building everything ourselves for full control and maintainability - similar to how we built:
- The MSE web player using hls.js internals
- The Electron mpv integration using koffi

### Native Module Architecture

Use React Native's **C++ TurboModule** system (New Architecture):
1. Shared C++ code wraps libmpv's C API
2. JSI provides direct JavaScript-to-C++ calls (no bridge overhead)
3. Platform-specific code only loads the library and registers the module

```
TypeScript API (MpvModule)
         │
         ▼
    JSI Bindings (C++)
         │
         ▼
  libmpv C API wrapper (C++)
         │
         ├──────────────────┐
         ▼                  ▼
   Android (JNI)        iOS (Objective-C++)
   loads .so            loads .framework
```

### Building libmpv from Source

We'll compile mpv/libmpv ourselves using mpv's build system. This gives us:
- Control over enabled features (audio-only, no video decoders)
- Smaller binary size
- Consistent behavior across platforms
- No dependency on third-party pre-built binaries

**Key dependencies to build:**
- FFmpeg (audio codecs: AAC, FLAC, MP3, Opus, Vorbis, ALAC, etc.)
- libmpv (audio player core)

**Not needed (audio-only):**
- libass (subtitles)
- video decoders
- GPU/OpenGL support

## Implementation Phases

### Phase 1: Build System Setup
1. Create unified build repository structure
2. Set up cross-compilation toolchains for all platforms
3. Write parameterized build scripts for FFmpeg (audio-only)
4. Write parameterized build scripts for libmpv (audio-only)

---

## Part A: Desktop Builds (Windows/macOS/Linux)

### Phase 2: Windows libmpv Build
1. Set up MSYS2/MinGW-w64 or cross-compile from Linux
2. Build FFmpeg for Windows x64 (audio decoders only)
3. Build libmpv for Windows linking against FFmpeg
4. Output: `libmpv-2.dll` + dependencies
5. Update Electron packaging to use new builds
6. Test in Electron app on Windows

### Phase 3: macOS libmpv Build
1. Build FFmpeg for macOS (arm64 + x86_64 universal)
2. Build libmpv for macOS linking against FFmpeg
3. Output: `libmpv.dylib` universal binary
4. Code sign for distribution
5. Update Electron packaging for macOS
6. Test in Electron app on macOS

### Phase 4: Linux libmpv Build
1. Build FFmpeg for Linux x64 (audio decoders only)
2. Build libmpv for Linux linking against FFmpeg
3. Output: `libmpv.so` with appropriate RPATH
4. Update Electron packaging for Linux
5. Test in Electron app on Linux (AppImage/deb)

---

## Part B: Mobile Builds (iOS/Android)

### Phase 5: Android libmpv Build
1. Cross-compile FFmpeg for Android (arm64-v8a, armeabi-v7a, x86_64)
2. Cross-compile libmpv for Android linking against FFmpeg
3. Create Android native module scaffold (Kotlin/Java)
4. Write JNI bindings to load and call libmpv
5. Create minimal TypeScript API: `initialize()`, `play(url)`, `destroy()`
6. Test basic playback on Android emulator

### Phase 6: iOS libmpv Build
1. Cross-compile FFmpeg for iOS (arm64 device + x86_64/arm64 simulator)
2. Cross-compile libmpv for iOS linking against FFmpeg
3. Package as .xcframework (multi-arch)
4. Create iOS native module (Objective-C++)
5. Mirror the Android API exactly
6. Test on iOS simulator, then physical device

---

## Part C: React Native Integration

### Phase 7: C++ TurboModule (Shared Code)
1. Create JSI spec defining the TypeScript interface
2. Write shared C++ wrapper around libmpv C API:
   - `mpv_create`, `mpv_initialize`, `mpv_destroy`
   - `mpv_command_string` for playback commands
   - `mpv_set_property`, `mpv_get_property`
   - `mpv_observe_property` for state changes
   - `mpv_wait_event` loop for async events
3. Platform-specific registration (OnLoad.cpp / ModuleProvider.mm)
4. Emit events back to JavaScript (similar to Electron IPC pattern)

### Phase 8: Full PlayerBackend API
Implement all methods matching your existing interface:
- `loadQueue(tracks, startIndex)` - Build mpv playlist
- `updateQueue(tracks, currentIndex)` - Reconcile queue changes
- `play/pause/togglePlayPause/seekTo/skip/playFromIndex`
- `setVolume/setRepeatMode`
- Property observation: `playlist-pos`, `pause`, `time-pos`, `duration`
- Events: `trackChanged`, `timeUpdate`, `playbackStateChanged`, `bufferingStateChanged`

### Phase 9: coral-app Integration
1. Create `mpv-native-proxy.ts` implementing `PlayerBackend`
2. Update `player-provider.native.tsx` to use it
3. Remove `expo-audio` and `expo-av` dependencies
4. Test gapless playback, background audio, media controls

---

## Part D: Testing & Polish

### Phase 10: Cross-Platform Testing
- Test across physical iOS and Android devices
- Test Electron on Windows, macOS, Linux
- Background audio stress testing
- Bluetooth/CarPlay/Android Auto testing
- Error handling and recovery
- App store compliance testing (iOS/Android/Mac App Store)

## First Concrete Steps

When ready to implement, start here:

1. **Clone mpv and FFmpeg sources** (for reference and building):
   ```bash
   git clone https://github.com/mpv-player/mpv.git --depth 1 ~/temp/mpv
   git clone https://github.com/FFmpeg/FFmpeg.git --depth 1 -b release/7.1 ~/temp/ffmpeg
   ```

2. **Study mpv's build system**:
   - Read `~/temp/mpv/meson.build` to understand configure options
   - Identify audio-only build flags
   - Understand libmpv output modes

3. **Set up Android NDK** (if not already):
   ```bash
   # Install via Android Studio SDK Manager or:
   sdkmanager "ndk;27.0.12077973"
   ```

4. **Create the package scaffold**:
   ```bash
   mkdir -p src/react-native-mpv/{android,ios,src,scripts}
   cd src/react-native-mpv && bun init
   ```

5. **Write FFmpeg cross-compile script** for Android (audio-only):
   - Target: arm64-v8a first
   - Enable: AAC, FLAC, MP3, Opus, Vorbis, ALAC decoders
   - Disable: all video, GPU, network protocols (mpv handles HTTP)

6. **Study reference build scripts** (for build patterns, not to use directly):
   - [FFmpeg-Kit](https://github.com/arthenica/ffmpeg-kit) - professional cross-compile scripts
   - [mpv-android buildscripts](https://github.com/mpv-android/mpv-android/tree/master/buildscripts)

## Build Requirements

### Host Machines
- **Windows (primary):** Native Windows builds + WSL2 for cross-compilation
- **Mac (available):** macOS + iOS builds, universal binaries
- **Linux (via WSL2 or VM):** Linux builds + Android cross-compilation

### Common Dependencies (All Platforms)
- Meson + Ninja (mpv build system)
- CMake (FFmpeg)
- pkg-config
- Git

### Windows Build Dependencies
- MSYS2 with MinGW-w64 toolchain
- Or: Cross-compile from Linux using mingw-w64

### macOS Build Dependencies
- Xcode + Command Line Tools
- Homebrew (for build tools)

### Linux Build Dependencies
- GCC/Clang toolchain
- Development headers (alsa-lib, pulseaudio)

### Android Build Dependencies
- Android NDK (r27+)
- Cross-compile from Linux/WSL2

### iOS Build Dependencies
- Xcode + Command Line Tools
- Cross-compile on macOS only

---

## Build Configurations

### FFmpeg Configure (Audio-Only Base)
```bash
# Common flags for all platforms
FFMPEG_COMMON_FLAGS="
  --disable-programs \
  --disable-doc \
  --disable-everything \
  --enable-decoder=aac,flac,mp3,opus,vorbis,alac,pcm_s16le,pcm_s24le,pcm_s32le,pcm_f32le \
  --enable-demuxer=mov,mp3,flac,ogg,wav,aac,matroska \
  --enable-protocol=file,http,https,hls,crypto \
  --enable-parser=aac,flac,mpegaudio,opus,vorbis \
  --enable-bsf=aac_adtstoasc \
  --enable-filter=aresample,volume \
  --enable-swresample \
  --disable-debug \
  --disable-stripping
"
```

### FFmpeg Platform-Specific

**Windows (MinGW-w64):**
```bash
./configure $FFMPEG_COMMON_FLAGS \
  --target-os=mingw32 \
  --arch=x86_64 \
  --cross-prefix=x86_64-w64-mingw32- \
  --enable-cross-compile
```

**macOS (Universal):**
```bash
# Build for each arch, then lipo -create
./configure $FFMPEG_COMMON_FLAGS \
  --enable-cross-compile \
  --target-os=darwin \
  --arch=arm64  # or x86_64
```

**Linux:**
```bash
./configure $FFMPEG_COMMON_FLAGS \
  --enable-shared \
  --enable-pic
```

**Android:**
```bash
./configure $FFMPEG_COMMON_FLAGS \
  --enable-cross-compile \
  --target-os=android \
  --arch=aarch64 \
  --cpu=armv8-a \
  --cc=$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android24-clang \
  --sysroot=$NDK/toolchains/llvm/prebuilt/linux-x86_64/sysroot
```

**iOS:**
```bash
./configure $FFMPEG_COMMON_FLAGS \
  --enable-cross-compile \
  --target-os=darwin \
  --arch=arm64 \
  --cc="xcrun -sdk iphoneos clang" \
  --extra-cflags="-arch arm64 -mios-version-min=15.0 -isysroot $(xcrun --sdk iphoneos --show-sdk-path)"
```

### libmpv Meson Configure (Audio-Only Base)
```bash
# Common meson options
LIBMPV_COMMON_OPTIONS="
  -Dlibmpv=true \
  -Dcplayer=false \
  -Dbuild-date=false \
  -Dgl=disabled \
  -Dvulkan=disabled \
  -Dcocoa=disabled \
  -Ddrm=disabled \
  -Dwayland=disabled \
  -Dx11=disabled \
  -Dmanpage-build=disabled \
  -Dhtml-build=disabled \
  -Dpdf-build=disabled
"
```

**Windows:**
```bash
meson setup build $LIBMPV_COMMON_OPTIONS \
  --cross-file=cross-mingw-x86_64.txt \
  -Dwin32-threads=enabled
```

**macOS:**
```bash
meson setup build $LIBMPV_COMMON_OPTIONS \
  -Dcoreaudio=enabled \
  -Dmacos-cocoa-cb=disabled \
  -Dmacos-media-player=disabled
```

**Linux:**
```bash
meson setup build $LIBMPV_COMMON_OPTIONS \
  -Dalsa=enabled \
  -Dpulse=enabled \
  -Dpipewire=enabled
```

**Android:**
```bash
meson setup build $LIBMPV_COMMON_OPTIONS \
  --cross-file=cross-android-arm64.txt \
  -Dandroid-media-ndk=enabled \
  -Dopensles=enabled
```

**iOS:**
```bash
meson setup build $LIBMPV_COMMON_OPTIONS \
  --cross-file=cross-ios-arm64.txt \
  -Dcoreaudio=enabled \
  -Daudiounit=enabled
```

## Technical Considerations

### Audio Focus (Android)
- Need to implement AudioFocusRequest in the native module
- Handle audio focus changes (duck, pause on call, etc.)
- mpv itself doesn't handle Android audio focus - we add it in the wrapper

### Background Audio (iOS)
- Already have `UIBackgroundModes: ["audio"]` in app.json
- Need to configure AVAudioSession in native module
- Categories: `.playback` with options `.mixWithOthers` or `.duckOthers`

### Audio Output
- **Android:** OpenSL ES or AAudio (mpv supports both via `--ao=`)
- **iOS:** AudioUnit (Core Audio) - mpv's coreaudio output

### Expo Compatibility
- Requires **development build** (not Expo Go)
- Already have `expo-dev-client` configured
- Native module integrates via autolinking

### Binary Size (Estimated)
With audio-only FFmpeg + libmpv (no video decoders, no GPU):

| Platform | Architecture | Estimated Size |
|----------|--------------|----------------|
| Windows | x64 | ~15-20MB |
| macOS | Universal (arm64 + x86_64) | ~25-35MB |
| Linux | x64 | ~12-18MB |
| Android | arm64-v8a | ~15-20MB |
| Android | armeabi-v7a | ~12-15MB |
| Android | x86_64 | ~15-20MB |
| iOS | arm64 | ~15-20MB |
| iOS Simulator | arm64 + x86_64 | ~25-35MB |

**Total app size impact:**
- Android APK: +40-55MB (all architectures)
- iOS IPA: +15-20MB (arm64 only for App Store)
- Electron: +15-35MB depending on platform

## Decisions Made

1. **Module structure:** Separate `@coral/react-native-mpv` package for mobile
2. **Build scripts:** Separate `src/libmpv-build` directory for all build scripts
3. **Video support:** Audio-only across all platforms (smaller binaries)
4. **Fallback strategy:** No fallback - remove expo-audio entirely on mobile
5. **HLS vs direct:** Stick with HLS for consistency with backend
6. **Desktop builds:** Drop-in replacements for existing Electron binaries

## Verification Plan

### All Platforms
1. **Basic playback test**: Play a track, verify audio output
2. **Gapless test**: Queue 2+ tracks, verify seamless transition
3. **Seek test**: Seek to various positions, verify accuracy
4. **Stress test**: Large queue, rapid skip, seek while buffering

### Desktop (Electron)
5. **Windows test**: Run on Windows 10/11, verify playback
6. **macOS test**: Run on macOS (Intel + Apple Silicon), verify playback
7. **Linux test**: Run on Ubuntu/Fedora, verify playback
8. **Media keys**: System media keys work on all desktop platforms

### Mobile (iOS/Android)
9. **Background test**: App backgrounded, verify continued playback
10. **Lock screen controls**: Lock screen/notification controls work
11. **Audio focus**: Phone call pauses music, resumes after (Android)
12. **Bluetooth/CarPlay**: External audio destinations work
13. **App store compliance**: Passes iOS App Store and Google Play review

## Project Structure

```
Coral/
├── src/
│   ├── coral-app/                         # Existing app
│   │   ├── electron/
│   │   │   └── native/
│   │   │       └── libmpv/
│   │   │           ├── win/              # libmpv-2.dll (from our builds)
│   │   │           ├── mac/              # libmpv.dylib (universal)
│   │   │           └── linux/            # libmpv.so
│   │   ├── lib/player/
│   │   │   ├── player-provider.native.tsx  # Update to use MpvNativeProxy
│   │   │   └── mpv-native-proxy.ts         # New - implements PlayerBackend
│   │   └── package.json
│   │
│   ├── react-native-mpv/                  # New package (mobile)
│   │   ├── android/
│   │   │   ├── src/main/
│   │   │   │   ├── java/                 # Kotlin/Java bridge
│   │   │   │   └── jniLibs/
│   │   │   │       ├── arm64-v8a/        # libmpv.so
│   │   │   │       ├── armeabi-v7a/      # libmpv.so
│   │   │   │       └── x86_64/           # libmpv.so
│   │   │   └── build.gradle
│   │   ├── ios/
│   │   │   ├── MpvModule.mm              # Objective-C++ bridge
│   │   │   └── Frameworks/
│   │   │       └── libmpv.xcframework/   # Multi-arch framework
│   │   ├── src/
│   │   │   └── index.ts                  # JS API
│   │   └── package.json
│   │
│   └── libmpv-build/                      # Build scripts repository
│       ├── scripts/
│       │   ├── build-ffmpeg.sh           # FFmpeg build script
│       │   ├── build-mpv.sh              # mpv build script
│       │   └── build-all.sh              # Orchestrates all builds
│       ├── cross-files/                   # Meson cross-compilation files
│       │   ├── cross-mingw-x86_64.txt
│       │   ├── cross-android-arm64.txt
│       │   ├── cross-android-armv7.txt
│       │   ├── cross-ios-arm64.txt
│       │   └── cross-ios-sim.txt
│       ├── patches/                       # Any necessary patches
│       ├── output/                        # Build artifacts
│       │   ├── windows-x64/
│       │   ├── macos-universal/
│       │   ├── linux-x64/
│       │   ├── android-arm64/
│       │   ├── android-armv7/
│       │   ├── android-x64/
│       │   ├── ios-arm64/
│       │   └── ios-sim/
│       └── README.md
```

## Files to Modify in coral-app

### Mobile (React Native)
| File | Change |
|------|--------|
| `lib/player/player-provider.native.tsx` | Replace expo-audio with MpvNativeProxy |
| `lib/player/mpv-native-proxy.ts` | New file - implements PlayerBackend using native module |
| `package.json` | Add `@coral/react-native-mpv`, remove `expo-audio`, `expo-av` |
| `app.json` | Remove expo-audio plugin |

### Desktop (Electron)
| File | Change |
|------|--------|
| `electron/native/libmpv/win/` | Replace with our custom-built `libmpv-2.dll` |
| `electron/native/libmpv/mac/` | Replace with our custom-built `libmpv.dylib` |
| `electron/native/libmpv/linux/` | Replace with our custom-built `libmpv.so` |
| `electron/mpv-bindings.mjs` | Update paths if needed, verify API compatibility |
| `electron-builder.json` | Update extraResources if paths change |

## Estimated Complexity

This is a **significant project** but well within your skillset given the MSE player and Electron mpv work:

| Phase | Effort | Notes |
|-------|--------|-------|
| Build System Setup | Medium | Cross-compile toolchains, scripts |
| Windows libmpv Build | Medium | MinGW cross-compile or MSYS2 native |
| macOS libmpv Build | Medium | Universal binary, code signing |
| Linux libmpv Build | Low-Medium | Most straightforward build |
| Android libmpv Build | Medium-High | NDK cross-compile, JNI basics |
| iOS libmpv Build | Medium-High | XCFramework, needs Mac |
| C++ TurboModule | Medium | JSI patterns once understood |
| Full API | Low-Medium | Port logic from Electron implementation |
| coral-app Integration | Low | PlayerBackend abstraction already exists |
| Testing | Medium-High | 6 platforms, real device testing |

**Key advantages:**
- Your Electron `mpv-player.mjs` is the blueprint - same mpv commands, property observation, and event handling
- Desktop builds are drop-in replacements for existing binaries
- Mobile builds share the same libmpv API, just different bindings
