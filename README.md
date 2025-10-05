# Coral

A brand new take on a self-hosted music streaming platform.

## Features

- A universal app written in React Native (Web, iOS, Android)
- Gapless audio playback on all platforms
- A recommendation system powered by [Essentia's](https://essentia.upf.edu/) ML models.
- On-demand, on-the-fly audio transcoding

## Build

- PostgreSQL with pgvector extension
- .NET 9
- Bun
- make

Install frontend dependencies.

'''bash
$ cd src/coral-app
$ bun install
'''

### Backend

'''bash
$ cd src/Coral.Essentia.Cli
$ make install-deps && make configure && make build
$ cd ../Coral.Api
$ dotnet run --
'''

### Web

bash'''
$ cd src/coral-app
$ bun run web
'''

### iOS

Download XCode from the app store.

Run the following command:

bash'''
$ sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
$ sudo xcodebuild -license
$ cd src/coral-app
$ bunx expo run:ios
'''

Open the `ios` directory in Xcode, look in the top bar where it says "iOS 18.2 Not installed" and click "Get".
