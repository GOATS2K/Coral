# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Coral is a self-hosted music streaming platform with:
- **coral-app**: React Native universal app (iOS/Android/Web) - primary and future UI
- **Coral.Frontend**: Next.js web application (legacy, being phased out in favor of coral-app)
- **Coral.Api**: C# ASP.NET Core backend API
- **Coral.Essentia.Cli**: C++ CLI for audio feature extraction (replacing Python prototype)

The backend uses PostgreSQL with pgvector for track embeddings and recommendations. Audio feature extraction for ML-based recommendations uses the Essentia C++ library via Coral.Essentia.Cli (cross-platform). A Python FastAPI prototype (Coral.Essentia.API) exists but is being phased out.

## Build & Development

### Backend (.NET)
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the API server (starts on configured port, typically https://localhost:7214)
dotnet run --project src/Coral.Api

# The API automatically runs database migrations on startup
```

### Frontend (React Native Universal App)
```bash
cd src/coral-app

# Install dependencies
bun install

# Development server
bun dev

# Platform-specific development
bun android  # Android emulator
bun ios      # iOS simulator (Mac only)
bun web      # Web browser

# Generate API client from backend OpenAPI spec
bun generate-client
```

### Legacy Next.js Frontend (Deprecated)
The Next.js frontend in `src/Coral.Frontend` is being replaced by the universal React Native app. For legacy development:
```bash
cd src/Coral.Frontend
npm install
npm run dev
npm run generate-client  # After API changes
```

## Testing

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run specific test project
dotnet test tests/Coral.Services.Tests
dotnet test tests/Coral.Encoders.Tests
dotnet test tests/Coral.Dto.Tests
```

Tests use xUnit with NSubstitute for mocking. The test projects include a Coral.TestProviders project for shared test utilities. Some tests use Testcontainers for PostgreSQL integration testing.

## Architecture

### Backend Structure

**Service Layer Pattern**:
- `Coral.Api`: ASP.NET Core controllers and startup configuration
- `Coral.Services`: Business logic layer (IndexerService, LibraryService, PlaybackService, TranscoderService, InferenceService, SearchService)
- `Coral.Database`: Entity Framework Core DbContext and migrations
- `Coral.Database.Models`: Database entity models
- `Coral.Dto`: Data transfer objects and AutoMapper profiles
- `Coral.Encoders`: Audio encoding/transcoding implementations with platform-specific encoder selection
- `Coral.Configuration`: Application configuration and directory management

**Key Services**:
- **IndexerService** (src/Coral.Services/IndexerService.cs): Scans music libraries, indexes audio files, extracts metadata, manages track embeddings
- **TranscoderService** (src/Coral.Services/TranscoderService.cs): Handles HLS transcoding for audio streaming
- **InferenceService** (src/Coral.Services/InferenceService.cs): Extracts audio embeddings via Coral.Essentia.Cli (C++) for recommendations
- **PlaybackService** (src/Coral.Services/PlaybackService.cs): Manages playback sessions and HLS playlist generation
- **EncoderFactory** (src/Coral.Encoders/EncoderFactory.cs): Platform-aware encoder instantiation for audio conversion

**Plugin System**:
- `Coral.PluginBase`: Plugin interfaces (IPlugin, IPluginService, IHostServiceProxy)
- `Coral.PluginHost`: Plugin loading and lifecycle management
- `Coral.Plugin.LastFM`: Example plugin for Last.fm integration
- Plugins can register services and controllers dynamically at runtime

**Events System**:
- `Coral.Events`: Event emitters for cross-component communication (TrackPlaybackEventEmitter, MusicLibraryRegisteredEventEmitter)
- Services use channels for background processing (IEmbeddingChannel)

### Database

PostgreSQL with pgvector extension for track embeddings:
- Connection configured in CoralDbContext (src/Coral.Database/CoralDbContext.cs:36)
- Default: `Host=localhost;Username=postgres;Password=admin;Database=coral2`
- HNSW index on TrackEmbedding.Embedding for cosine similarity search
- Migrations run automatically on application startup (src/Coral.Api/Program.cs:108)

### Audio Feature Extraction Pipeline

Coral.Essentia.Cli (C++) handles audio feature extraction using the Essentia library:
- Cross-platform CLI tool that uses TensorFlow models for audio analysis
- Extracts 256-dimensional embeddings for track recommendations
- InferenceService invokes this CLI tool as a subprocess
- Embeddings stored in PostgreSQL with pgvector for cosine similarity search
- Legacy Python prototype (Coral.Essentia.API) exists but is being replaced

### Frontend Architecture

**Universal App (coral-app)** - Primary UI:
- React Native with Expo for iOS/Android/Web
- Expo Router for file-based navigation
- NativeWind (Tailwind CSS) for styling
- React Native Reusables component library
- Auto-generated TypeScript API client from OpenAPI spec
- Uses Jotai for state management
- **Theme management**: Always use `themeAtom` from `@/lib/state` for color scheme instead of `useColorScheme()` hook

**React State Management Patterns:**
- **Prefer Jotai atoms for app-wide state**: Player state, theme, any state shared across components or that persists across navigation
- **Use `useState` for component-local UI state**: Hover states, drag states, form inputs that don't need to be shared
- **Minimize `useRef` usage**: Only for DOM references, timers/intervals, or mutable values that shouldn't trigger re-renders
- **Be consistent**: If already using Jotai for a domain (like player), continue using it rather than mixing in refs or local state

**Legacy Next.js (Coral.Frontend)** - Being phased out:
- React Query for API state management
- Mantine UI component library
- Will be replaced by coral-app's web target

### Audio Streaming

HLS (HTTP Live Streaming) pipeline:
- Audio files transcoded to HLS segments on-demand
- Segments stored in ApplicationConfiguration.HLSDirectory
- Static file serving at /hls endpoint (src/Coral.Api/Program.cs:78-90)
- Custom content-type provider for .m3u8 and .m4s files
- No caching on HLS chunks for proper streaming behavior

## OpenAPI Integration

The coral-app universal frontend generates TypeScript clients from the backend OpenAPI spec:

1. Backend exposes OpenAPI at `/swagger` in development
2. OpenAPI JSON written to `src/Coral.Api/openapi.json`
3. coral-app references this file in `openapi-codegen.config.ts`
4. Run `bun generate-client` in coral-app after API changes
5. Legacy Next.js frontend has similar setup but will be deprecated

The backend uses custom operation IDs based on controller method names for cleaner client code.

## Development Workflow

1. **Backend Changes**: After modifying controllers or DTOs, rebuild to regenerate OpenAPI spec, then run `bun generate-client` in coral-app
2. **Database Changes**: Create migrations with `dotnet ef migrations add <name> --project src/Coral.Database`, they apply automatically on next API startup
3. **New Audio Encoders**: Implement IEncoder with EncoderFrontendAttribute, factory will auto-discover based on platform
4. **New Plugins**: Implement IPlugin interface, place in plugin directory, PluginInitializer worker loads on startup
5. **Audio Feature Extraction**: Coral.Essentia.Cli handles all audio analysis, invoked by InferenceService as a subprocess

## Platform Notes

- **Coral.Essentia.Cli**: C++ CLI tool for audio feature extraction, targeting cross-platform support (currently in development)
- **macOS/Linux/Windows**: Encoder availability varies by platform (see EncoderFactory)
- **.NET 9.0** required for backend
- **Bun** recommended for coral-app development
- **Legacy Python ML API**: Coral.Essentia.API prototype exists but will be fully replaced by Coral.Essentia.Cli

## Notes to Claude
- When you are repeatedly struggling with fixing the same bug, take a step back, evaluate your implementation from top to bottom and focus on any architectural issues that may be the cause - rather than applying quick band-aid fixes.
- When bugs re-appear, immediately start adding logging to the problematic areas of the codebase.
- Follow the KISS principle whereever possible.