# Makefile for Coral.Essentia.Cli cross-platform builds
# Handles dependency installation and CMake build configuration

# Detect platform
ifeq ($(OS),Windows_NT)
    PLATFORM := Windows
    ESSENTIA_RELEASE := essentia-full-windows-x64.zip
else
    UNAME_S := $(shell uname -s)
    ifeq ($(UNAME_S),Linux)
        PLATFORM := Linux
        ESSENTIA_RELEASE := essentia-ubuntu-amd64.zip
    endif
    ifeq ($(UNAME_S),Darwin)
        PLATFORM := macOS
        ESSENTIA_RELEASE := essentia-macos-arm64.zip
    endif
endif

# GitHub release URL
ESSENTIA_RELEASE_URL := https://github.com/GOATS2K/essentia-builds/releases/latest/download/$(ESSENTIA_RELEASE)
TENSORFLOW_VERSION := 2.18.0
TENSORFLOW_URL := https://storage.googleapis.com/tensorflow/versions/$(TENSORFLOW_VERSION)/libtensorflow-cpu-linux-x86_64.tar.gz

.PHONY: all install-deps install-deps-linux install-deps-macos install-deps-windows configure build clean help

all: help

help:
	@echo "Coral.Essentia.Cli Build System"
	@echo "================================"
	@echo "Platform detected: $(PLATFORM)"
	@echo ""
	@echo "Available targets:"
	@echo "  install-deps    - Install platform-specific dependencies"
	@echo "  configure       - Configure CMake build"
	@echo "  build          - Build the project"
	@echo "  clean          - Clean build artifacts"
	@echo ""
	@echo "Quick start:"
	@echo "  make install-deps && make configure && make build"

# Main dependency installation target
install-deps:
ifeq ($(PLATFORM),Linux)
	$(MAKE) install-deps-linux
else ifeq ($(PLATFORM),macOS)
	$(MAKE) install-deps-macos
else ifeq ($(PLATFORM),Windows)
	$(MAKE) install-deps-windows
else
	@echo "Unknown platform: $(PLATFORM)"
	@exit 1
endif

# Linux (Ubuntu/Debian) dependency installation
install-deps-linux:
	@echo "Installing dependencies for Linux..."
	sudo apt-get update
	sudo apt-get install -y \
		libeigen3-dev \
		libyaml-dev \
		libfftw3-dev \
		libavcodec-dev \
		libavformat-dev \
		libavutil-dev \
		libswresample-dev \
		libsamplerate0-dev \
		libtag1-dev \
		libchromaprint-dev \
		wget \
		unzip \
		cmake \
		build-essential
	@echo "Downloading TensorFlow C library..."
	wget -q --no-check-certificate $(TENSORFLOW_URL) -O /tmp/libtensorflow.tar.gz
	sudo tar -C /usr/local -xzf /tmp/libtensorflow.tar.gz
	sudo ldconfig /usr/local/lib
	rm /tmp/libtensorflow.tar.gz
	@echo "Downloading Essentia from releases..."
	wget -q --no-check-certificate $(ESSENTIA_RELEASE_URL) -O /tmp/$(ESSENTIA_RELEASE)
	mkdir -p lib include
	unzip -o /tmp/$(ESSENTIA_RELEASE) -d /tmp/essentia-extract
	cp -r /tmp/essentia-extract/lib/* lib/
	cp -r /tmp/essentia-extract/include/* include/
	rm -rf /tmp/$(ESSENTIA_RELEASE) /tmp/essentia-extract
	@echo "Linux dependencies installed successfully!"

# macOS dependency installation
install-deps-macos:
	@echo "Installing dependencies for macOS..."
	brew install \
		eigen \
		libyaml \
		fftw \
		ffmpeg \
		libsamplerate \
		libtag \
		chromaprint \
		libtensorflow \
		cmake \
		wget
	@echo "Downloading Essentia from releases..."
	wget -q --no-check-certificate $(ESSENTIA_RELEASE_URL) -O /tmp/$(ESSENTIA_RELEASE)
	mkdir -p lib include
	unzip -o /tmp/$(ESSENTIA_RELEASE) -d /tmp/essentia-extract
	cp -r /tmp/essentia-extract/lib/* lib/
	cp -r /tmp/essentia-extract/include/* include/
	rm -rf /tmp/$(ESSENTIA_RELEASE) /tmp/essentia-extract
	@echo "macOS dependencies installed successfully!"

# Windows dependency installation (download if not present)
install-deps-windows:
	@echo "Checking Windows dependencies..."
	@powershell -Command " \
		if (-Not (Test-Path 'lib')) { \
			Write-Host 'Downloading Essentia from releases...'; \
			Start-BitsTransfer -Source '$(ESSENTIA_RELEASE_URL)' -Destination 'essentia.zip' -Description 'Downloading Essentia'; \
			Write-Host 'Extracting archive...'; \
			Expand-Archive -Path 'essentia.zip' -DestinationPath '.' -Force; \
			Remove-Item 'essentia.zip'; \
			Write-Host 'Windows dependencies installed successfully!'; \
		} else { \
			Write-Host 'Dependencies already present. Skipping download.'; \
		}"

# Configure CMake build
configure:
	cmake -B build -S .

# Build the project
build:
	cmake --build build --config Debug

# Clean build artifacts
clean:
	rm -rf build
ifeq ($(PLATFORM),Windows)
	if exist build rmdir /s /q build
endif
