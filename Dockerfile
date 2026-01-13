# Stage 1: Frontend Build (Node + Bun for package management)
# Metro bundler requires Node.js worker threads, so we use Node as runtime
# but Bun for fast package installation
FROM node:22-slim AS frontend-build
RUN npm install -g bun
WORKDIR /app/coral-app
COPY src/coral-app/package.json src/coral-app/bun.lock* ./
# Use Bun for fast install, trust postinstalls for native deps (SWC, etc.)
RUN bun install --frozen-lockfile && bun pm trust @swc/core unrs-resolver --all
COPY src/coral-app/ ./
# Use bun run which delegates to Node for Metro bundler compatibility
RUN bun run web:export

# Stage 2: Essentia CLI Build
# Using Ubuntu Noble (24.04) because the pre-built Essentia library requires FFmpeg 6.x
FROM ubuntu:noble AS essentia-build
# Install build dependencies (same as Makefile but without sudo)
RUN apt-get update && apt-get install -y \
    build-essential cmake wget pkg-config curl unzip \
    libeigen3-dev libyaml-dev libfftw3-dev \
    libavcodec-dev libavformat-dev libavutil-dev libswresample-dev \
    libsamplerate0-dev libtag1-dev libchromaprint-dev

# Install TensorFlow C library
RUN wget -q --no-check-certificate https://storage.googleapis.com/tensorflow/versions/2.18.0/libtensorflow-cpu-linux-x86_64.tar.gz && \
    tar -C /usr/local -xzf libtensorflow-cpu-linux-x86_64.tar.gz && \
    ldconfig /usr/local/lib && \
    rm libtensorflow-cpu-linux-x86_64.tar.gz

WORKDIR /app
# Copy entire Essentia CLI directory
COPY src/Coral.Essentia.Cli/ ./
# Download Essentia release (Makefile uses sudo which isn't available in Docker)
RUN wget -q --no-check-certificate https://github.com/GOATS2K/essentia-builds/releases/latest/download/essentia-ubuntu-amd64.zip -O /tmp/essentia.zip && \
    mkdir -p lib include && \
    unzip -o /tmp/essentia.zip -d /tmp/essentia-extract && \
    cp -r /tmp/essentia-extract/lib/* lib/ && \
    cp -r /tmp/essentia-extract/include/* include/ && \
    rm -rf /tmp/essentia.zip /tmp/essentia-extract
# Configure and build
RUN make configure && make build

# Stage 3: Backend Build (.NET SDK)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src
# Copy everything and restore
COPY . .
RUN dotnet restore
# Run tests to ensure build quality
RUN dotnet test --no-restore --verbosity normal
# Copy frontend build to wwwroot
COPY --from=frontend-build /app/coral-app/dist/ src/Coral.Api/wwwroot/
# Publish with skip flags (frontend/essentia built in dedicated Docker stages)
RUN dotnet publish src/Coral.Api/Coral.Api.csproj -c Release -o /app/publish \
    -p:SkipFrontendBuild=true -p:SkipEssentiaBuild=true -p:SkipSwaggerGen=true --no-restore

# Stage 4: Runtime Image
# Using Ubuntu Noble (24.04) for FFmpeg 6.x compatibility with pre-built Essentia
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime
# Install runtime dependencies for Essentia CLI (Ubuntu Noble / FFmpeg 6.x versions)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfftw3-double3 libfftw3-single3 libavcodec60 libavformat60 libavutil58 libswresample4 \
    libsamplerate0 libtag1v5 libchromaprint1 libyaml-0-2 wget ffmpeg && \
    rm -rf /var/lib/apt/lists/*

# Install TensorFlow C library (same as build stage)
RUN wget -q --no-check-certificate https://storage.googleapis.com/tensorflow/versions/2.18.0/libtensorflow-cpu-linux-x86_64.tar.gz && \
    tar -C /usr/local -xzf libtensorflow-cpu-linux-x86_64.tar.gz && \
    ldconfig /usr/local/lib && \
    rm libtensorflow-cpu-linux-x86_64.tar.gz

WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=essentia-build /app/build/Coral.Essentia.Cli ./
COPY --from=essentia-build /app/lib/* /usr/local/lib/
RUN ldconfig
# Create directories:
# - /config: for config.json (detected via /.dockerenv)
# - /data: default data directory (configurable in config.json:Paths.Data)
# - /music: mount point for music library
RUN mkdir -p /config /data /music && touch /.dockerenv
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000
ENTRYPOINT ["./Coral.Api"]
