# Coral

A brand new take on a self-hosted music streaming platform.

## Features

- A universal app written in React Native (Web, iOS, Android)
- Gapless audio playback on all platforms
- A recommendation system powered by [Essentia's](https://essentia.upf.edu/) ML models.
- On-demand, on-the-fly audio transcoding

## Build

- .NET 9
- Bun
- make
- ffmpeg

Install frontend dependencies.

```bash
$ cd src/coral-app
$ bun install
```

### Backend
You will need to have a C++ toolchain setup to compile the inference program. 

On Windows, you can [install MSVC](https://code.visualstudio.com/docs/cpp/config-msvc#_prerequisites) to do that. 

On Linux, you should be fine with the `build-essential` package.

 On macOS, you'll need `brew` installed.

```bash
$ cd src/Coral.Essentia.Cli
$ make install-deps 
$ make configure
$ make build
$ cd ../Coral.Api
$ dotnet run --
```

### Web

```bash
$ cd src/coral-app
$ bun run web
```

It is recommended to use Firefox as Chrome is much stricter with what codecs it supports when using MSE.

### Electron
First, download [libmpv](https://sourceforge.net/projects/mpv-player-windows/files/libmpv/) for x86_64 and extract the files to `src/coral-app/electron/native/libmpv/win`

Then, in `src/coral-app` run `bun run electron:dev` and refresh the page with `CTRL+SHIFT+R` to kick off the web server.

### iOS

Download XCode from the app store.

Run the following command:

```bash
$ sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
$ sudo xcodebuild -license
$ cd src/coral-app
$ bunx expo run:ios
```

Open the `ios` directory in Xcode, look in the top bar where it says "iOS 18.2 Not installed" and click "Get".
