# Coral.Essentia.Cli

Cross-platform CLI tool for audio feature extraction using the Essentia library and TensorFlow.

## Features

- Extracts 256-dimensional embeddings from audio files
- Uses Essentia's TensorflowPredictEffnetDiscogs algorithm
- Cross-platform support (Windows, macOS, Linux)
- UTF-8 filename support on all platforms

## Prerequisites

### All Platforms
- CMake 3.15 or higher
- C++17 compatible compiler

### Platform-Specific

**Windows:**
- Visual Studio 2019/2022 with C++ build tools (or MinGW/Clang)
- Dependencies are bundled in the repository

**macOS:**
- Xcode Command Line Tools
- Homebrew package manager

**Linux (Ubuntu/Debian):**
- GCC or Clang
- apt package manager (sudo access required)

## Building

### Quick Start

```bash
# Install dependencies
make install-deps

# Configure CMake
make configure

# Build
make build

# The executable will be in build/Debug/ or build/Release/
```

### Detailed Instructions

#### Windows

On Windows, dependencies are already included in the repository. If you need to re-download them:

```bash
make install-deps-windows
```

Then build:

```bash
cmake -B build -S .
cmake --build build --config Debug
```

The executable will be in `build/Debug/Coral.Essentia.Cli.exe`

#### macOS

Install dependencies via Homebrew:

```bash
make install-deps-macos
```

This will install:
- Eigen (linear algebra library)
- YAML-CPP
- FFTW3
- FFmpeg (avcodec, avformat, avutil, swresample)
- libsamplerate
- TagLib
- Chromaprint
- TensorFlow C library
- Essentia (from GitHub releases)

Then build:

```bash
cmake -B build -S .
cmake --build build
```

The executable will be in `build/Coral.Essentia.Cli`

#### Linux (Ubuntu/Debian)

Install dependencies via apt and download Essentia:

```bash
make install-deps-linux
```

This will install:
- Development libraries for audio processing (FFmpeg, FFTW, etc.)
- TensorFlow C library (downloaded from official releases)
- Essentia (from GitHub releases)

Then build:

```bash
cmake -B build -S .
cmake --build build
```

The executable will be in `build/Coral.Essentia.Cli`

## Usage

```bash
Coral.Essentia.Cli <audio_file> <model_path> <output_file>
```

**Arguments:**
- `audio_file` - Path to the audio file to analyze
- `model_path` - Path to the TensorFlow model file (.pb)
- `output_file` - Path where embeddings will be written

**Example:**

```bash
./Coral.Essentia.Cli /path/to/song.mp3 /path/to/model.pb /path/to/output.txt
```

## Output Format

The tool generates a text file with the following structure:

```
-- Inference Result --
<embedding values, one per line>

-- Inference Data --
Row count: <number of embedding vectors>
Embedding size: <dimension of each vector>
```

## Dependencies

### Core Libraries
- **Essentia** - Audio analysis library
- **TensorFlow** - Machine learning inference
- **Eigen3** - Linear algebra (header-only)
- **FFmpeg** - Audio decoding (avcodec, avformat, avutil, swresample)
- **FFTW3** - Fast Fourier Transform
- **libsamplerate** - Audio resampling
- **TagLib** - Audio metadata
- **Chromaprint** - Audio fingerprinting
- **YAML-CPP** - Configuration parsing

### Dependency Sources

- **Essentia builds**: https://github.com/GOATS2K/essentia-builds/releases
- **TensorFlow**: https://www.tensorflow.org/install/lang_c
- All other dependencies are installed via system package managers

## Troubleshooting

### Library not found errors (Linux/macOS)

If you get runtime errors about missing libraries:

```bash
# Linux: Update library cache
sudo ldconfig

# macOS: Check Homebrew library paths
export DYLD_LIBRARY_PATH=/opt/homebrew/lib:$DYLD_LIBRARY_PATH
```

### CMake can't find Essentia

Make sure you ran `make install-deps` before `cmake configure`. The Makefile downloads Essentia to the `lib/` and `include/` directories.

### Visual Studio build (Windows)

If you prefer to use Visual Studio instead of CMake, the original `.vcxproj` file is still available in the repository.

## Development

### Build Configurations

```bash
# Debug build (default)
cmake -B build -S . -DCMAKE_BUILD_TYPE=Debug
cmake --build build

# Release build
cmake -B build -S . -DCMAKE_BUILD_TYPE=Release
cmake --build build
```

### Clean Build

```bash
make clean
# or manually
rm -rf build/
```

## License

This project is part of the Coral music streaming platform.
