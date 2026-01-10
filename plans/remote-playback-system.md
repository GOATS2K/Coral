# Remote Playback System (Spotify Connect)

## Overview

Spotify Connect-style feature allowing users to control playback on remote devices. Users can seamlessly transfer playback between their own devices or use one device to control another.

## Architecture Model

**Single Active Player with Multiple Controllers:**

```
                              SERVER
  ┌────────────────────────────────────────────────────────────┐
  │  Tracks: activePlayerId, connectedDevices                  │
  │  Receives state from active player → can trigger Last.fm   │
  │  Relays state to all connected devices                     │
  │  Routes commands to active player                          │
  └────────────────────────────────────────────────────────────┘
         ▲ state              │ state              │ state
         │                    ▼                    ▼
    ┌────┴─────┐        ┌───────────┐        ┌───────────┐
    │ Device A │        │ Device B  │        │ Device C  │
    │ (active) │◀──cmd──│(controller│◀──cmd──│(controller│
    │ playing  │        │  or idle) │        │  or idle) │
    └──────────┘        └───────────┘        └───────────┘
```

**Key Rules:**
1. **One active player** - only one device plays audio at a time
2. **Broadcasting on playback** - when a device plays anything, it becomes active and broadcasts state
3. **Multiple controllers** - any connected device can send commands to the active player
4. **Transfer = takeover** - transferring to a new device copies state, new device becomes active, old device auto-pauses
5. **Server-side scrobbling** - server receives state updates, can trigger Last.fm integration

## Implementation Decisions

- **Auth**: Single-user mode (no authentication) - all devices share implicit user
- **Device ID**: localStorage + UUID (generated on first load)
- **Device Naming**: Electron uses hostname, web uses "$browser on $OS" (via navigator.userAgent)
- **Platform**: Web-only first (native platforms deferred)
- **Connection**: Auto-connect to SignalR hub on app load (devices always visible)
- **Active player is source of truth**: Device playing audio owns state, broadcasts to server

## Codebase Integration Notes

### Backend
- SignalR already configured in `Program.cs` (line 37)
- Existing `LibraryHub` at `/hubs/library` provides pattern to follow
- `ScanReporter` shows how to broadcast via `IHubContext<THub, TClient>`
- New hub should be singleton (like `IScanReporter`)

### Frontend
- Player state managed via Jotai atoms in `lib/state.ts`
- `playerStateAtom` (queue/track) and `playbackStateAtom` (position/playing)
- Platform-specific player providers in `lib/player/player-provider.web.tsx`
- Player events already emit via `PlayerEvent` enum - hook state sync here

## Key Principles

- **Single active player**: Only one device plays audio at a time
- **Stateless on disconnect**: If the active player disconnects, playback session ends for controllers
- **Seamless handoff**: Transfer playback copies state (queue, position) to new device
- **Universal client**: Single React Native codebase works across all platforms (Web/Electron/iOS/Android)

## Architecture

### Backend (ASP.NET Core + SignalR)

#### PlaybackHub (SignalR Hub)

```csharp
namespace Coral.Api.Hubs;

public interface IPlaybackHubClient
{
    Task DeviceConnected(RemoteDeviceDto device);
    Task DeviceDisconnected(string deviceId);
    Task ActivePlayerChanged(string? deviceId, PlaybackStateDto? state);
    Task PlaybackStateUpdated(PlaybackStateDto state);
    Task ReceiveCommand(PlaybackCommandDto command);
    Task TransferRequested(PlaybackStateDto state);
}

public class PlaybackHub : Hub<IPlaybackHubClient>
{
    private readonly IPlaybackCoordinatorService _coordinator;

    public PlaybackHub(IPlaybackCoordinatorService coordinator)
    {
        _coordinator = coordinator;
    }

    // Client -> Server: Register device on connection
    public async Task RegisterDevice(string deviceId, string deviceName)
    {
        _coordinator.RegisterDevice(deviceId, deviceName, Context.ConnectionId);

        // Send current device list and active player to new connection
        var devices = _coordinator.GetAllDevices();
        var activePlayerId = _coordinator.GetActivePlayerId();
        var activeState = _coordinator.GetActivePlayerState();

        // Notify all other devices about new connection
        await Clients.Others.DeviceConnected(new RemoteDeviceDto
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            IsActivePlayer = false
        });

        // Send current active player state to caller if exists
        if (activePlayerId != null)
        {
            await Clients.Caller.ActivePlayerChanged(activePlayerId, activeState);
        }
    }

    // Client -> Server: Device starts playing, becomes active player
    public async Task BecomeActivePlayer(PlaybackStateDto state)
    {
        var deviceId = _coordinator.GetDeviceIdByConnection(Context.ConnectionId);
        if (deviceId == null) return;

        _coordinator.SetActivePlayer(deviceId, state);

        // Notify all devices about new active player
        await Clients.All.ActivePlayerChanged(deviceId, state);
    }

    // Client -> Server: Active player broadcasts state update
    public async Task UpdatePlaybackState(PlaybackStateDto state)
    {
        var deviceId = _coordinator.GetDeviceIdByConnection(Context.ConnectionId);
        var activePlayerId = _coordinator.GetActivePlayerId();

        // Only accept state updates from active player
        if (deviceId != activePlayerId) return;

        _coordinator.UpdateActivePlayerState(state);

        // Broadcast to all other devices
        await Clients.Others.PlaybackStateUpdated(state);
    }

    // Client -> Server: Send command to active player
    public async Task SendCommand(PlaybackCommandDto command)
    {
        var activePlayerId = _coordinator.GetActivePlayerId();
        if (activePlayerId == null) return;

        var activeConnection = _coordinator.GetConnectionId(activePlayerId);
        if (activeConnection == null) return;

        await Clients.Client(activeConnection).ReceiveCommand(command);
    }

    // Client -> Server: Transfer playback to another device
    public async Task TransferPlayback(string targetDeviceId)
    {
        var currentState = _coordinator.GetActivePlayerState();
        var activePlayerId = _coordinator.GetActivePlayerId();

        if (currentState == null) return;

        // Pause current active player
        if (activePlayerId != null)
        {
            var activeConnection = _coordinator.GetConnectionId(activePlayerId);
            if (activeConnection != null)
            {
                await Clients.Client(activeConnection).ReceiveCommand(new PlaybackCommandDto
                {
                    Type = PlaybackCommandType.Pause
                });
            }
        }

        // Tell target device to start playing with current state
        var targetConnection = _coordinator.GetConnectionId(targetDeviceId);
        if (targetConnection != null)
        {
            await Clients.Client(targetConnection).TransferRequested(currentState);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = _coordinator.GetDeviceIdByConnection(Context.ConnectionId);
        var wasActivePlayer = deviceId == _coordinator.GetActivePlayerId();

        _coordinator.UnregisterDevice(Context.ConnectionId);

        if (deviceId != null)
        {
            // Notify others about disconnection
            await Clients.Others.DeviceDisconnected(deviceId);

            // If active player disconnected, clear active player state
            if (wasActivePlayer)
            {
                await Clients.All.ActivePlayerChanged(null, null);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

#### PlaybackCoordinatorService

Manages in-memory device registry and active player state.

```csharp
namespace Coral.Services;

public interface IPlaybackCoordinatorService
{
    // Device management
    void RegisterDevice(string deviceId, string deviceName, string connectionId);
    void UnregisterDevice(string connectionId);
    string? GetConnectionId(string deviceId);
    string? GetDeviceIdByConnection(string connectionId);
    List<RemoteDeviceDto> GetAllDevices();

    // Active player management
    string? GetActivePlayerId();
    void SetActivePlayer(string deviceId, PlaybackStateDto state);
    void ClearActivePlayer();
    void UpdateActivePlayerState(PlaybackStateDto state);
    PlaybackStateDto? GetActivePlayerState();
}

public class PlaybackCoordinatorService : IPlaybackCoordinatorService
{
    // Device registry: deviceId -> DeviceInfo
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();

    // Reverse lookup: connectionId -> deviceId
    private readonly ConcurrentDictionary<string, string> _connections = new();

    // Active player state
    private string? _activePlayerId;
    private PlaybackStateDto? _activePlayerState;
    private readonly object _activePlayerLock = new();

    public void RegisterDevice(string deviceId, string deviceName, string connectionId)
    {
        _devices[deviceId] = new DeviceInfo
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            ConnectionId = connectionId
        };
        _connections[connectionId] = deviceId;
    }

    public void UnregisterDevice(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var deviceId))
        {
            _devices.TryRemove(deviceId, out _);

            // Clear active player if it was this device
            lock (_activePlayerLock)
            {
                if (_activePlayerId == deviceId)
                {
                    _activePlayerId = null;
                    _activePlayerState = null;
                }
            }
        }
    }

    public string? GetConnectionId(string deviceId)
    {
        return _devices.TryGetValue(deviceId, out var info) ? info.ConnectionId : null;
    }

    public string? GetDeviceIdByConnection(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var deviceId) ? deviceId : null;
    }

    public List<RemoteDeviceDto> GetAllDevices()
    {
        return _devices.Values.Select(d => new RemoteDeviceDto
        {
            DeviceId = d.DeviceId,
            DeviceName = d.DeviceName,
            IsActivePlayer = d.DeviceId == _activePlayerId
        }).ToList();
    }

    public string? GetActivePlayerId()
    {
        lock (_activePlayerLock)
        {
            return _activePlayerId;
        }
    }

    public void SetActivePlayer(string deviceId, PlaybackStateDto state)
    {
        lock (_activePlayerLock)
        {
            _activePlayerId = deviceId;
            _activePlayerState = state;
        }
    }

    public void ClearActivePlayer()
    {
        lock (_activePlayerLock)
        {
            _activePlayerId = null;
            _activePlayerState = null;
        }
    }

    public void UpdateActivePlayerState(PlaybackStateDto state)
    {
        lock (_activePlayerLock)
        {
            _activePlayerState = state;
        }
    }

    public PlaybackStateDto? GetActivePlayerState()
    {
        lock (_activePlayerLock)
        {
            return _activePlayerState;
        }
    }
}

public class DeviceInfo
{
    public required string DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required string ConnectionId { get; init; }
}
```

### Models

```csharp
namespace Coral.Dto.RemotePlayback;

public class PlaybackStateDto
{
    public Guid? CurrentTrackId { get; set; }
    public int PositionMs { get; set; }
    public bool IsPlaying { get; set; }
    public List<Guid> Queue { get; set; } = new();
    public int CurrentIndex { get; set; }
    public bool IsShuffled { get; set; }
    public RepeatMode RepeatMode { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum RepeatMode
{
    Off,
    All,
    One
}

public class PlaybackCommandDto
{
    public PlaybackCommandType Type { get; set; }
    public JsonElement? Payload { get; set; }
}

public enum PlaybackCommandType
{
    // Playback control
    Play,
    Pause,
    Skip,
    Previous,
    Seek,              // Payload: { positionMs: int }

    // Mode control
    ToggleShuffle,     // No payload
    SetRepeatMode,     // Payload: { mode: RepeatMode }

    // Queue mutations
    AddToQueue,        // Payload: { trackIds: Guid[] }
    RemoveFromQueue,   // Payload: { index: int }
    ReorderQueue,      // Payload: { fromIndex: int, toIndex: int }
    SetQueue           // Payload: { queue: Guid[], startIndex: int }
}

public class RemoteDeviceDto
{
    public required string DeviceId { get; set; }
    public required string DeviceName { get; set; }
    public bool IsActivePlayer { get; set; }
}
```

### SignalR Configuration (Program.cs)

```csharp
// Add SignalR
builder.Services.AddSignalR();

// Add PlaybackCoordinatorService as singleton (in-memory state)
builder.Services.AddSingleton<IPlaybackCoordinatorService, PlaybackCoordinatorService>();

// ...

app.MapHub<PlaybackHub>("/hubs/playback");
```

## Frontend (React Native Universal)

### State Management (Jotai Atoms)

```typescript
// lib/state/remote-playback.ts
import { atom } from 'jotai';
import { getDeviceId } from '@/lib/device';

// Types
interface PlaybackState {
  currentTrackId: string | null;
  positionMs: number;
  isPlaying: boolean;
  queue: string[];
  currentIndex: number;
  isShuffled: boolean;
  repeatMode: 'off' | 'all' | 'one';
  timestamp: Date;
}

interface RemoteDevice {
  deviceId: string;
  deviceName: string;
  isActivePlayer: boolean;
}

// Connected devices (including self)
export const connectedDevicesAtom = atom<RemoteDevice[]>([]);

// Active player info (null if nothing playing anywhere)
export const activePlayerIdAtom = atom<string | null>(null);
export const activePlayerStateAtom = atom<PlaybackState | null>(null);

// Derived: Am I the active player?
export const isActivePlayerAtom = atom((get) => {
  const activeId = get(activePlayerIdAtom);
  return activeId === getDeviceId();
});

// Derived: Is there a remote active player I can control?
export const remoteActivePlayerAtom = atom((get) => {
  const activeId = get(activePlayerIdAtom);
  const devices = get(connectedDevicesAtom);
  if (!activeId || activeId === getDeviceId()) return null;
  return devices.find(d => d.deviceId === activeId) ?? null;
});

// Derived: Current playback state (local or remote)
export const currentPlaybackStateAtom = atom((get) => {
  const isActive = get(isActivePlayerAtom);
  if (isActive) {
    // We're the active player, use local state
    return get(localPlaybackStateAtom);
  } else {
    // Remote device is playing, use their state
    return get(activePlayerStateAtom);
  }
});
```

### SignalR Connection

```typescript
// lib/signalr/playback-hub.ts
import * as SignalR from '@microsoft/signalr';
import { getDeviceId, getDeviceName } from '@/lib/device';

class PlaybackHubClient {
  private connection: SignalR.HubConnection | null = null;

  async connect() {
    this.connection = new SignalR.HubConnectionBuilder()
      .withUrl('/hubs/playback')
      .withAutomaticReconnect()
      .build();

    // Server -> Client: Another device connected
    this.connection.on('DeviceConnected', (device: RemoteDevice) => {
      // Add to connected devices atom
      addConnectedDevice(device);
    });

    // Server -> Client: Device disconnected
    this.connection.on('DeviceDisconnected', (deviceId: string) => {
      removeConnectedDevice(deviceId);
    });

    // Server -> Client: Active player changed
    this.connection.on('ActivePlayerChanged', (deviceId: string | null, state: PlaybackState | null) => {
      setAtomValue(activePlayerIdAtom, deviceId);
      setAtomValue(activePlayerStateAtom, state);
    });

    // Server -> Client: Active player state updated
    this.connection.on('PlaybackStateUpdated', (state: PlaybackState) => {
      setAtomValue(activePlayerStateAtom, state);
    });

    // Server -> Client: Receive command (we are the active player)
    this.connection.on('ReceiveCommand', async (command: PlaybackCommand) => {
      await executePlaybackCommand(command);
    });

    // Server -> Client: Transfer playback to us
    this.connection.on('TransferRequested', async (state: PlaybackState) => {
      // Load the transferred state and start playing
      await loadAndPlayState(state);
    });

    await this.connection.start();

    // Register this device
    await this.connection.invoke('RegisterDevice', getDeviceId(), getDeviceName());
  }

  // Called when local device starts playing
  async becomeActivePlayer(state: PlaybackState) {
    await this.connection?.invoke('BecomeActivePlayer', state);
  }

  // Called by active player to sync state (debounced)
  async updatePlaybackState(state: PlaybackState) {
    await this.connection?.invoke('UpdatePlaybackState', state);
  }

  // Send command to active player (whoever it is)
  async sendCommand(command: PlaybackCommand) {
    await this.connection?.invoke('SendCommand', command);
  }

  // Transfer playback to another device
  async transferPlayback(targetDeviceId: string) {
    await this.connection?.invoke('TransferPlayback', targetDeviceId);
  }
}

export const playbackHub = new PlaybackHubClient();
```

### Player Integration

The critical design principle: **Listen to player events, not UI events.**

All state synchronization should be triggered by the underlying player backend (mpv, AVPlayer, ExoPlayer, MSE), not by UI button handlers. This ensures state updates are sent regardless of WHO initiated the change:
- Local user interaction
- Remote control commands
- AirPlay receiver commands
- Media keys (keyboard, headphones)
- System media controls

```typescript
// lib/player/remote-playback.ts

// Initialize player event listeners
export function initializePlayerStateSync() {
  // Listen to player backend events (platform-specific)
  playerBackend.on('trackChanged', () => scheduleStateSync());
  playerBackend.on('playbackStateChanged', () => scheduleStateSync());
  playerBackend.on('seeked', () => scheduleStateSync());
  playerBackend.on('queueChanged', () => scheduleStateSync());

  // Periodic position updates (every 1-2 seconds during playback)
  setInterval(() => {
    if (playerBackend.isPlaying()) {
      scheduleStateSync();
    }
  }, 1000);
}

// Called when local playback starts (user plays a track locally)
async function onLocalPlaybackStarted() {
  const state = getCurrentPlaybackState();
  await playbackHub.becomeActivePlayer(state);
}

// Called when player backend state changes (debounced)
async function syncPlaybackState() {
  const isActive = getAtomValue(isActivePlayerAtom);

  // Only sync if we're the active player
  if (isActive) {
    const state: PlaybackState = {
      currentTrackId: getCurrentTrack()?.id ?? null,
      positionMs: await getPlaybackPosition(),
      isPlaying: isPlaying(),
      queue: getQueue().map(track => track.id),
      currentIndex: getCurrentIndex(),
      isShuffled: getIsShuffled(),
      repeatMode: getRepeatMode(),
      timestamp: new Date()
    };

    await playbackHub.updatePlaybackState(state);
  }
}

// Execute command received from a controller
export async function executePlaybackCommand(command: PlaybackCommand) {
  switch (command.type) {
    // Playback control
    case 'Play':
      await play();
      break;
    case 'Pause':
      await pause();
      break;
    case 'Skip':
      await skipToNext();
      break;
    case 'Previous':
      await skipToPrevious();
      break;
    case 'Seek':
      await seek(command.payload.positionMs);
      break;

    // Mode control
    case 'ToggleShuffle':
      await toggleShuffle();
      break;
    case 'SetRepeatMode':
      await setRepeatMode(command.payload.mode);
      break;

    // Queue mutations
    case 'AddToQueue':
      await addToQueue(command.payload.trackIds);
      break;
    case 'RemoveFromQueue':
      await removeFromQueue(command.payload.index);
      break;
    case 'ReorderQueue':
      await reorderQueue(command.payload.fromIndex, command.payload.toIndex);
      break;
    case 'SetQueue':
      await setQueue(command.payload.queue, command.payload.startIndex);
      break;
  }
  // State sync happens automatically via player event listeners
}

// Load transferred state and start playing
export async function loadAndPlayState(state: PlaybackState) {
  // Load the queue
  await loadQueue(state.queue, state.currentIndex);

  // Restore shuffle and repeat modes
  if (state.isShuffled) {
    await setShuffle(true);
  }
  await setRepeatMode(state.repeatMode);

  // Seek to position
  await seek(state.positionMs);

  // Start playing (this triggers becomeActivePlayer via event listener)
  if (state.isPlaying) {
    await play();
  }
}

// Transfer playback to another device
export async function transferToDevice(targetDeviceId: string) {
  await playbackHub.transferPlayback(targetDeviceId);
}
```

### UI Components

#### Device Picker Modal

```typescript
// components/DevicePickerModal.tsx

export function DevicePickerModal() {
  const devices = useAtomValue(connectedDevicesAtom);
  const activePlayerId = useAtomValue(activePlayerIdAtom);
  const isActivePlayer = useAtomValue(isActivePlayerAtom);
  const activeState = useAtomValue(activePlayerStateAtom);
  const currentDeviceId = getDeviceId();

  return (
    <Modal>
      <Text>Select playback device</Text>

      {/* Current device */}
      <DeviceItem
        name="This device"
        isActivePlayer={isActivePlayer}
        onPress={() => transferToDevice(currentDeviceId)}
      />

      {/* Other connected devices */}
      {devices
        .filter(d => d.deviceId !== currentDeviceId)
        .map(device => (
          <DeviceItem
            key={device.deviceId}
            name={device.deviceName}
            isActivePlayer={device.deviceId === activePlayerId}
            trackInfo={device.deviceId === activePlayerId ? activeState : null}
            onPress={() => transferToDevice(device.deviceId)}
          />
        ))}
    </Modal>
  );
}

function DeviceItem({ name, isActivePlayer, trackInfo, onPress }: Props) {
  return (
    <Pressable onPress={onPress} style={[styles.item, isActivePlayer && styles.active]}>
      <Icon name="speaker" />
      <View>
        <Text style={{ fontWeight: isActivePlayer ? 'bold' : 'normal' }}>{name}</Text>
        {isActivePlayer && trackInfo && (
          <Text style={{ fontSize: 12 }}>Playing: {trackInfo.currentTrackId}</Text>
        )}
      </View>
    </Pressable>
  );
}
```

### Player UI Updates

The UI layer reads from derived atoms that abstract whether playback is local or remote:

```typescript
// components/Player.tsx
import { useAtomValue } from 'jotai';
import {
  activePlayerIdAtom,
  activePlayerStateAtom,
  isActivePlayerAtom,
  remoteActivePlayerAtom
} from '@/lib/state/remote-playback';

export function Player() {
  const isActivePlayer = useAtomValue(isActivePlayerAtom);
  const remotePlayer = useAtomValue(remoteActivePlayerAtom);
  const activeState = useAtomValue(activePlayerStateAtom);

  // Show banner when remote device is the active player
  const showRemoteBanner = remotePlayer !== null;

  return (
    <View>
      {/* Banner when viewing remote player state */}
      {showRemoteBanner && (
        <RemotePlayerBanner
          deviceName={remotePlayer.deviceName}
          onTransferPress={() => transferToDevice(getDeviceId())}
        />
      )}

      <TrackInfo track={activeState?.currentTrackId} />
      <ProgressBar position={activeState?.positionMs} />
      <PlayPauseButton isPlaying={activeState?.isPlaying} onPress={handlePlayPause} />
    </View>
  );
}

// Banner showing "Playing on {device}" with transfer option
function RemotePlayerBanner({ deviceName, onTransferPress }: Props) {
  return (
    <View style={{ backgroundColor: 'blue', padding: 12 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        <Icon name="speaker" />
        <Text style={{ fontWeight: 'bold' }}>
          Playing on {deviceName}
        </Text>
      </View>
      <Button onPress={onTransferPress}>
        Play on this device
      </Button>
    </View>
  );
}

// Playback controls route to active player
async function handlePlayPause() {
  const isActive = getAtomValue(isActivePlayerAtom);
  const isPlaying = getAtomValue(activePlayerStateAtom)?.isPlaying;

  if (isActive) {
    // We are the active player - control locally
    await togglePlayPause();
  } else {
    // Remote device is active - send command
    await playbackHub.sendCommand({
      type: isPlaying ? 'Pause' : 'Play'
    });
  }
}
```

**Key points:**

When **this device is the active player**:
- UI reads from local player state
- Local audio plays normally
- Control buttons interact with local player
- State is broadcast to other devices

When **remote device is the active player**:
- UI reads from `activePlayerStateAtom` (remote state)
- **No local audio playback** - this device is silent
- All control buttons send SignalR commands to active player
- Progress bar, track info update from remote state broadcasts
- **Display prominent "Playing on {device name}" banner**

## State Synchronization Strategy

### Debounced Updates

To avoid flooding the server with state updates:

```typescript
let stateUpdateTimer: NodeJS.Timeout | null = null;

export function scheduleStateSync() {
  if (stateUpdateTimer) {
    clearTimeout(stateUpdateTimer);
  }

  stateUpdateTimer = setTimeout(async () => {
    await syncPlaybackState();
  }, 1000); // Debounce 1 second
}
```

**Platform-Specific Event Listeners:**

```typescript
// Electron/mpv: Hook into mpv property observers
mpv.observeProperty('pause', () => scheduleStateSync());
mpv.observeProperty('time-pos', () => scheduleStateSync());
mpv.observeProperty('playlist-pos', () => scheduleStateSync());

// iOS/macOS: AVPlayer KVO observers
player.addObserver(forKeyPath: "timeControlStatus") { scheduleStateSync() }
player.addObserver(forKeyPath: "currentItem") { scheduleStateSync() }

// Android: ExoPlayer listeners
player.addListener(object : Player.Listener {
  override fun onPlaybackStateChanged(state: Int) { scheduleStateSync() }
  override fun onMediaItemTransition(item: MediaItem?) { scheduleStateSync() }
})

// Web/MSE: HTML5 Audio events
audio.addEventListener('play', () => scheduleStateSync());
audio.addEventListener('pause', () => scheduleStateSync());
audio.addEventListener('seeked', () => scheduleStateSync());
audio.addEventListener('ended', () => scheduleStateSync());
```

This ensures that **any** state change - whether from UI, remote control, AirPlay, or system media controls - triggers a state sync.

### State Reset on Disconnect

If the remote device disconnects (SignalR sends `DeviceUnavailable`):
1. Remote controller automatically switches back to local mode
2. Optionally: show toast "Lost connection to {device}"
3. Jukebox mode device continues playing (doesn't stop)

## Security Considerations

1. **User isolation**: SignalR Hub validates user ID from JWT token
2. **Command validation**: Only users can control their own devices
3. **Connection authentication**: JWT token required for WebSocket connection
4. **No state persistence**: All state is in-memory (privacy)

## Platform Differences

All platforms use the same SignalR client code:

- **Web (MSE)**: Standard SignalR JS client
- **Electron (mpv)**: SignalR JS client in renderer process, hooks into mpv property observers
- **iOS/Android**: SignalR via React Native, hooks into native player events

Platform-specific player backends (mpv, MSE, ExoPlayer, AVPlayer) are abstracted behind a common interface. Each backend exposes events like `trackChanged`, `playbackStateChanged`, `seeked` that trigger state synchronization.

### AirPlay Integration

When AirPlay is active:

- **AirPlay Receiver** (Electron/mpv receiving from iOS device): mpv property observers detect state changes made by AirPlay sender → state sync works automatically
- **AirPlay Sender** (iOS sending to Apple TV/HomePod): AVPlayer KVO observers detect state changes → state sync works automatically

The key is that we listen to the **player's** state, not the control interface. So whether the player is controlled by local UI, remote SignalR commands, AirPlay, or media keys - we always sync state.

## Future Enhancements

- [ ] Volume control command
- [ ] Multi-room sync (play same music on multiple devices simultaneously)
- [ ] Last.fm scrobbling from server based on active player state
- [ ] Guest access (temporary device linking for parties)
- [ ] Offline mode detection (graceful degradation)
- [ ] Device groups (control multiple devices as one)
- [ ] Native platform support (iOS, Android, Electron)

## Implementation Checklist

### Backend
- [ ] Create `src/Coral.Api/Hubs/PlaybackHub.cs` with `IPlaybackHubClient` interface
- [ ] Create `src/Coral.Services/PlaybackCoordinatorService.cs` (singleton, active player + device registry)
- [ ] Create `src/Coral.Dto/RemotePlayback/` folder with DTOs:
  - [ ] `PlaybackStateDto.cs`
  - [ ] `PlaybackCommandDto.cs`
  - [ ] `RemoteDeviceDto.cs`
- [ ] Register hub and service in `src/Coral.Api/Program.cs`

### Frontend
- [ ] Install `@microsoft/signalr` package
- [ ] Create `src/coral-app/lib/device.ts` - device ID (localStorage + UUID) and name utilities
- [ ] Create `src/coral-app/lib/state/remote-playback.ts` - Jotai atoms:
  - [ ] `connectedDevicesAtom`
  - [ ] `activePlayerIdAtom` / `activePlayerStateAtom`
  - [ ] `isActivePlayerAtom` / `remoteActivePlayerAtom` (derived)
- [ ] Create `src/coral-app/lib/signalr/playback-hub.ts` - SignalR client singleton
- [ ] Create `src/coral-app/lib/signalr/playback-hub-provider.tsx` - React provider
- [ ] Modify `src/coral-app/lib/player/player-provider.web.tsx`:
  - [ ] Call `becomeActivePlayer()` when playback starts
  - [ ] Call `updatePlaybackState()` on state changes (debounced)
  - [ ] Handle `ReceiveCommand` and `TransferRequested`
- [ ] Create `src/coral-app/components/player/device-picker.tsx`
- [ ] Modify `src/coral-app/components/player/web-player-bar.tsx`:
  - [ ] Add device picker button
  - [ ] Show "Playing on {device}" banner when remote is active
  - [ ] Route controls based on `isActivePlayerAtom`

## Testing Scenarios

1. **Device discovery**: Open app in two tabs, both appear in device picker
2. **Active player broadcast**: Play on Tab A, Tab B sees state updates
3. **Remote control**: From Tab B, send play/pause to Tab A
4. **Transfer playback**: From Tab B, transfer to self - A pauses, B starts playing
5. **Disconnect handling**: Close Tab A while active - Tab B sees ActivePlayerChanged(null)
6. **Multiple controllers**: Tab B and C both send commands to Tab A
7. **Network reconnect**: Disconnect/reconnect, state recovers correctly
