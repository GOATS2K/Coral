# Remote Playback System (Spotify Connect)

## Overview

Spotify Connect-style feature allowing users to control playback on remote devices. Users can seamlessly transfer playback between their own devices or use one device to control another as a "jukebox".

## Key Principles

- **User-scoped**: Users can only see and control their own devices
- **Stateless on disconnect**: If the controlled device disconnects, remote control session ends
- **Seamless handoff**: Clients detect active playback on other devices and offer control/takeover options
- **Universal client**: Single React Native codebase works across all platforms (Web/Electron/iOS/Android)

## Architecture

### Backend (ASP.NET Core + SignalR)

#### PlaybackHub (SignalR Hub)

```csharp
namespace Coral.Api.Hubs;

[Authorize]
public class PlaybackHub : Hub
{
    private readonly IPlaybackCoordinatorService _coordinator;
    private readonly IDeviceService _deviceService;

    // Client -> Server: Announce device is ready to receive commands
    public async Task RegisterForRemoteControl(string deviceId)
    {
        var userId = GetCurrentUserId();
        await _coordinator.RegisterDevice(userId, deviceId, Context.ConnectionId);

        // Notify user's other devices that this device is available
        await Clients.User(userId.ToString()).SendAsync("DeviceAvailable", new {
            deviceId,
            deviceName = await _deviceService.GetDeviceName(userId, deviceId)
        });
    }

    // Client -> Server: Send playback state update
    public async Task UpdatePlaybackState(string deviceId, PlaybackState state)
    {
        var userId = GetCurrentUserId();
        await _coordinator.UpdatePlaybackState(userId, deviceId, state);

        // Broadcast to user's other devices
        await Clients.User(userId.ToString()).SendAsync("PlaybackStateUpdated", deviceId, state);
    }

    // Client -> Server: Send command to remote device
    public async Task SendCommand(string targetDeviceId, PlaybackCommand command)
    {
        var userId = GetCurrentUserId();
        var targetConnection = await _coordinator.GetDeviceConnection(userId, targetDeviceId);

        if (targetConnection == null)
        {
            await Clients.Caller.SendAsync("CommandFailed", new {
                error = "Device not available",
                deviceId = targetDeviceId
            });
            return;
        }

        // Send command only to target device
        await Clients.Client(targetConnection).SendAsync("ReceiveCommand", command);
    }

    // Client -> Server: Request current state from a device
    public async Task RequestPlaybackState(string targetDeviceId)
    {
        var userId = GetCurrentUserId();
        var targetConnection = await _coordinator.GetDeviceConnection(userId, targetDeviceId);

        if (targetConnection != null)
        {
            await Clients.Client(targetConnection).SendAsync("StateRequested", Context.ConnectionId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        var deviceId = await _coordinator.GetDeviceIdByConnection(Context.ConnectionId);

        if (deviceId != null)
        {
            await _coordinator.UnregisterDevice(userId, deviceId);

            // Notify user's other devices
            await Clients.User(userId.ToString()).SendAsync("DeviceUnavailable", deviceId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim!.Value);
    }
}
```

#### PlaybackCoordinatorService

Manages in-memory device registry and playback state.

```csharp
namespace Coral.Services;

public interface IPlaybackCoordinatorService
{
    Task RegisterDevice(Guid userId, string deviceId, string connectionId);
    Task UnregisterDevice(Guid userId, string deviceId);
    Task UpdatePlaybackState(Guid userId, string deviceId, PlaybackState state);
    Task<PlaybackState?> GetPlaybackState(Guid userId, string deviceId);
    Task<string?> GetDeviceConnection(Guid userId, string deviceId);
    Task<string?> GetDeviceIdByConnection(string connectionId);
    Task<List<ActiveDevice>> GetUserActiveDevices(Guid userId);
}

public class PlaybackCoordinatorService : IPlaybackCoordinatorService
{
    // In-memory registry: userId -> (deviceId -> ActiveDevice)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ActiveDevice>> _registry = new();

    // Reverse lookup: connectionId -> (userId, deviceId)
    private readonly ConcurrentDictionary<string, (Guid userId, string deviceId)> _connections = new();

    public Task RegisterDevice(Guid userId, string deviceId, string connectionId)
    {
        var userDevices = _registry.GetOrAdd(userId, _ => new ConcurrentDictionary<string, ActiveDevice>());

        userDevices[deviceId] = new ActiveDevice
        {
            DeviceId = deviceId,
            ConnectionId = connectionId,
            LastSeen = DateTime.UtcNow
        };

        _connections[connectionId] = (userId, deviceId);

        return Task.CompletedTask;
    }

    public Task UnregisterDevice(Guid userId, string deviceId)
    {
        if (_registry.TryGetValue(userId, out var userDevices))
        {
            if (userDevices.TryRemove(deviceId, out var device))
            {
                _connections.TryRemove(device.ConnectionId, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdatePlaybackState(Guid userId, string deviceId, PlaybackState state)
    {
        if (_registry.TryGetValue(userId, out var userDevices) &&
            userDevices.TryGetValue(deviceId, out var device))
        {
            device.PlaybackState = state;
            device.LastSeen = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<PlaybackState?> GetPlaybackState(Guid userId, string deviceId)
    {
        if (_registry.TryGetValue(userId, out var userDevices) &&
            userDevices.TryGetValue(deviceId, out var device))
        {
            return Task.FromResult(device.PlaybackState);
        }

        return Task.FromResult<PlaybackState?>(null);
    }

    public Task<string?> GetDeviceConnection(Guid userId, string deviceId)
    {
        if (_registry.TryGetValue(userId, out var userDevices) &&
            userDevices.TryGetValue(deviceId, out var device))
        {
            return Task.FromResult<string?>(device.ConnectionId);
        }

        return Task.FromResult<string?>(null);
    }

    public Task<string?> GetDeviceIdByConnection(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var mapping))
        {
            return Task.FromResult<string?>(mapping.deviceId);
        }

        return Task.FromResult<string?>(null);
    }

    public Task<List<ActiveDevice>> GetUserActiveDevices(Guid userId)
    {
        if (_registry.TryGetValue(userId, out var userDevices))
        {
            return Task.FromResult(userDevices.Values.ToList());
        }

        return Task.FromResult(new List<ActiveDevice>());
    }
}

public class ActiveDevice
{
    public string DeviceId { get; set; } = null!;
    public string ConnectionId { get; set; } = null!;
    public PlaybackState? PlaybackState { get; set; }
    public DateTime LastSeen { get; set; }
}
```

### Models

```csharp
namespace Coral.Dto.RemotePlayback;

public class PlaybackState
{
    public Guid? CurrentTrackId { get; set; }
    public int PositionMs { get; set; }
    public bool IsPlaying { get; set; }
    public List<QueueItem> Queue { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class QueueItem
{
    public Guid TrackId { get; set; }
    public int Index { get; set; }
}

public class PlaybackCommand
{
    public PlaybackCommandType Type { get; set; }
    public object? Payload { get; set; }
}

public enum PlaybackCommandType
{
    Play,
    Pause,
    Seek,           // Payload: { positionMs: number }
    Skip,
    Previous,
    AppendToQueue,  // Payload: { trackIds: Guid[] }
    SetQueue,       // Payload: { queue: QueueItem[], startIndex: number }
    SetVolume       // Payload: { volume: number }
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

### Playback Modes

The app operates in one of three modes:

1. **Local Playback** - Playing music on this device
2. **Remote Control** - Controlling another device
3. **Jukebox Mode** - Being controlled by another device

### State Management (Jotai Atoms)

```typescript
// lib/state/remote-playback.ts
import { atom } from 'jotai';

export const playbackModeAtom = atom<'local' | 'remote' | 'jukebox'>('local');

// When in remote control mode, this is the device being controlled
export const remoteDeviceAtom = atom<RemoteDevice | null>(null);

// List of available devices for this user
export const availableDevicesAtom = atom<RemoteDevice[]>([]);

// Last known state from remote device (when in remote control mode)
export const remotePlaybackStateAtom = atom<PlaybackState | null>(null);

// Derived atom: Get the current playback state (local or remote)
export const currentPlaybackStateAtom = atom((get) => {
  const mode = get(playbackModeAtom);

  if (mode === 'remote') {
    // When controlling a remote device, use remote state
    return get(remotePlaybackStateAtom);
  } else {
    // When playing locally or in jukebox mode, use local player state
    return get(localPlayerStateAtom);
  }
});

// Derived atom: Get the current track
export const currentTrackAtom = atom((get) => {
  const state = get(currentPlaybackStateAtom);
  return state?.currentTrackId ? getTrackById(state.currentTrackId) : null;
});

// Derived atom: Is currently playing?
export const isPlayingAtom = atom((get) => {
  const state = get(currentPlaybackStateAtom);
  return state?.isPlaying ?? false;
});

// Derived atom: Current playback position
export const playbackPositionAtom = atom((get) => {
  const state = get(currentPlaybackStateAtom);
  return state?.positionMs ?? 0;
});

// Derived atom: Current queue
export const currentQueueAtom = atom((get) => {
  const state = get(currentPlaybackStateAtom);
  return state?.queue ?? [];
});
```

### SignalR Connection

```typescript
// lib/signalr/playback-hub.ts
import * as SignalR from '@microsoft/signalr';
import { getToken } from '@/lib/auth';

class PlaybackHubClient {
  private connection: SignalR.HubConnection | null = null;
  private deviceId: string;

  constructor() {
    this.deviceId = getDeviceId(); // From AsyncStorage
  }

  async connect() {
    const token = await getToken();

    this.connection = new SignalR.HubConnectionBuilder()
      .withUrl('https://your-api/hubs/playback', {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build();

    // Server -> Client: Another device is available
    this.connection.on('DeviceAvailable', (device: RemoteDevice) => {
      // Add to available devices
      addAvailableDevice(device);
    });

    // Server -> Client: Device disconnected
    this.connection.on('DeviceUnavailable', (deviceId: string) => {
      removeAvailableDevice(deviceId);

      // If we were controlling this device, reset to local
      const currentRemoteDevice = getAtomValue(remoteDeviceAtom);
      if (currentRemoteDevice?.deviceId === deviceId) {
        setAtomValue(playbackModeAtom, 'local');
        setAtomValue(remoteDeviceAtom, null);
      }
    });

    // Server -> Client: Playback state updated from another device
    this.connection.on('PlaybackStateUpdated', (deviceId: string, state: PlaybackState) => {
      const currentRemoteDevice = getAtomValue(remoteDeviceAtom);

      // If this is the device we're controlling, update remote state
      // This triggers UI updates even though no audio is playing locally
      if (currentRemoteDevice?.deviceId === deviceId) {
        setAtomValue(remotePlaybackStateAtom, state);
        // UI components reading from currentPlaybackStateAtom will update automatically
      }

      // Check if another device started playing (for takeover prompt)
      if (deviceId !== this.deviceId && state.isPlaying) {
        showPlaybackActiveNotification(deviceId);
      }
    });

    // Server -> Client: Receive command from another device
    this.connection.on('ReceiveCommand', async (command: PlaybackCommand) => {
      await executePlaybackCommand(command);

      // After executing command, send updated state back
      const state = getCurrentPlaybackState();
      await this.updatePlaybackState(state);
    });

    // Server -> Client: Another device requested our state
    this.connection.on('StateRequested', async (requesterId: string) => {
      const state = getCurrentPlaybackState();
      await this.updatePlaybackState(state);
    });

    await this.connection.start();

    // Register this device for remote control
    await this.registerForRemoteControl();
  }

  async registerForRemoteControl() {
    await this.connection?.invoke('RegisterForRemoteControl', this.deviceId);
  }

  async updatePlaybackState(state: PlaybackState) {
    await this.connection?.invoke('UpdatePlaybackState', this.deviceId, state);
  }

  async sendCommand(targetDeviceId: string, command: PlaybackCommand) {
    await this.connection?.invoke('SendCommand', targetDeviceId, command);
  }

  async requestPlaybackState(targetDeviceId: string) {
    await this.connection?.invoke('RequestPlaybackState', targetDeviceId);
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

// Called when player backend state changes
async function syncPlaybackState() {
  const mode = getAtomValue(playbackModeAtom);

  // Only sync if we're the active player (local or jukebox)
  // Don't sync if we're in remote control mode
  if (mode === 'local' || mode === 'jukebox') {
    const state: PlaybackState = {
      currentTrackId: getCurrentTrack()?.id ?? null,
      positionMs: await getPlaybackPosition(),
      isPlaying: isPlaying(),
      queue: getQueue().map((track, index) => ({ trackId: track.id, index })),
      timestamp: new Date()
    };

    await playbackHub.updatePlaybackState(state);
  }
}

// Execute command received from remote controller
export async function executePlaybackCommand(command: PlaybackCommand) {
  // Mark as jukebox mode when we receive first command
  const currentMode = getAtomValue(playbackModeAtom);
  if (currentMode !== 'jukebox') {
    setAtomValue(playbackModeAtom, 'jukebox');
  }

  switch (command.type) {
    case 'Play':
      await play();
      break;
    case 'Pause':
      await pause();
      break;
    case 'Seek':
      await seek(command.payload.positionMs);
      break;
    case 'Skip':
      await skipToNext();
      break;
    case 'Previous':
      await skipToPrevious();
      break;
    case 'AppendToQueue':
      await appendToQueue(command.payload.trackIds);
      break;
    case 'SetQueue':
      await setQueue(command.payload.queue, command.payload.startIndex);
      break;
    case 'SetVolume':
      await setVolume(command.payload.volume);
      break;
  }
}

// When user wants to control a remote device
export async function connectToRemoteDevice(device: RemoteDevice) {
  // Stop local playback
  await pause();

  // Switch to remote control mode
  setAtomValue(playbackModeAtom, 'remote');
  setAtomValue(remoteDeviceAtom, device);

  // Request current state from remote device
  await playbackHub.requestPlaybackState(device.deviceId);
}

// When user wants to take over playback locally
export async function takeoverPlayback() {
  setAtomValue(playbackModeAtom, 'local');
  setAtomValue(remoteDeviceAtom, null);
  setAtomValue(remotePlaybackStateAtom, null);

  // Optionally: send pause command to other playing devices
  const activeDevices = getAtomValue(availableDevicesAtom).filter(d => d.isPlaying);
  for (const device of activeDevices) {
    await playbackHub.sendCommand(device.deviceId, { type: 'Pause' });
  }
}
```

### UI Components

#### Device Picker Modal

```typescript
// components/DevicePickerModal.tsx

export function DevicePickerModal() {
  const availableDevices = useAtomValue(availableDevicesAtom);
  const currentMode = useAtomValue(playbackModeAtom);
  const remoteDevice = useAtomValue(remoteDeviceAtom);
  const currentDeviceId = getDeviceId();

  return (
    <Modal>
      <Text>Select playback device</Text>

      {/* Current device */}
      <DeviceItem
        name="This device"
        icon={getDeviceIcon(currentDeviceId)}
        active={currentMode === 'local'}
        onPress={() => takeoverPlayback()}
      />

      {/* Other available devices */}
      {availableDevices.map(device => (
        <DeviceItem
          key={device.deviceId}
          name={device.deviceName}
          icon={getDeviceIcon(device.deviceId)}
          active={currentMode === 'remote' && remoteDevice?.deviceId === device.deviceId}
          isPlaying={device.isPlaying}
          onPress={() => connectToRemoteDevice(device)}
        />
      ))}
    </Modal>
  );
}

function DeviceItem({ name, icon, active, isPlaying, onPress }: Props) {
  return (
    <Pressable onPress={onPress} style={[styles.item, active && styles.active]}>
      <Icon name={icon} />
      <View>
        <Text style={{ fontWeight: active ? 'bold' : 'normal' }}>{name}</Text>
        {isPlaying && !active && <Text style={{ fontSize: 12 }}>Currently playing</Text>}
        {active && <Text style={{ fontSize: 12, color: 'green' }}>Active</Text>}
      </View>
    </Pressable>
  );
}
```

#### Playback Active Notification

When another device starts playing, show a subtle notification:

```typescript
// components/PlaybackActiveNotification.tsx

export function PlaybackActiveNotification({ deviceName }: { deviceName: string }) {
  return (
    <Notification>
      <Text>Playing on {deviceName}</Text>
      <Button onPress={() => connectToRemoteDevice(device)}>
        Control
      </Button>
      <Button onPress={() => takeoverPlayback()}>
        Play here
      </Button>
    </Notification>
  );
}
```

### Player UI Updates

The UI layer should be completely agnostic to playback mode - it always reads from the derived atoms:

```typescript
// components/Player.tsx
import { useAtomValue } from 'jotai';
import {
  currentTrackAtom,
  isPlayingAtom,
  playbackPositionAtom,
  currentQueueAtom,
  playbackModeAtom,
  remoteDeviceAtom
} from '@/lib/state/remote-playback';

export function Player() {
  const currentTrack = useAtomValue(currentTrackAtom);
  const isPlaying = useAtomValue(isPlayingAtom);
  const position = useAtomValue(playbackPositionAtom);
  const queue = useAtomValue(currentQueueAtom);
  const mode = useAtomValue(playbackModeAtom);
  const remoteDevice = useAtomValue(remoteDeviceAtom);

  // UI always reflects current state, whether local or remote
  return (
    <View>
      {/* Prominent indicator when controlling a remote device */}
      {mode === 'remote' && (
        <RemoteControlBanner
          deviceName={remoteDevice?.deviceName}
          onTakeoverPress={() => takeoverPlayback()}
        />
      )}

      <TrackInfo track={currentTrack} />
      <ProgressBar position={position} duration={currentTrack?.duration} />
      <PlayPauseButton isPlaying={isPlaying} onPress={handlePlayPause} />
      <Queue items={queue} />
    </View>
  );
}

// Prominent banner to show user they're controlling a remote device
function RemoteControlBanner({ deviceName, onTakeoverPress }: Props) {
  return (
    <View style={{ backgroundColor: 'blue', padding: 12 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        <Icon name="speaker" />
        <Text style={{ fontWeight: 'bold' }}>
          Playing on {deviceName}
        </Text>
      </View>
      <Button onPress={onTakeoverPress}>
        Play on this device
      </Button>
    </View>
  );
}

// Playback controls route to either local player or remote command
async function handlePlayPause() {
  const mode = getAtomValue(playbackModeAtom);

  if (mode === 'remote') {
    const remoteDevice = getAtomValue(remoteDeviceAtom);
    const isPlaying = getAtomValue(isPlayingAtom);

    await playbackHub.sendCommand(remoteDevice.deviceId, {
      type: isPlaying ? 'Pause' : 'Play'
    });
  } else {
    // Local playback
    await togglePlayPause();
  }
}
```

**Key points:**

When in **remote control mode** (controlling a jukebox):
- UI reads from `remotePlaybackStateAtom` via derived atoms
- **No local audio playback** - this device is silent
- All control buttons send SignalR commands instead of controlling local player
- Progress bar, track info, queue display all update from remote state broadcasts
- **Display prominent "Playing on {device name}" indicator** so user knows they're controlling a remote device
- UI updates in real-time as jukebox state changes

When in **local/jukebox mode**:
- UI reads from `localPlayerStateAtom` via derived atoms
- Local audio plays normally
- Control buttons interact with local player
- No special UI indicators needed (plays like normal)

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

- [ ] Transfer playback state when switching devices (continue queue on new device)
- [ ] Multi-room sync (play same music on multiple devices simultaneously)
- [ ] Guest access (temporary device linking for parties)
- [ ] Offline mode detection (graceful degradation)
- [ ] Device groups (control multiple devices as one)

## Implementation Checklist

### Backend
- [ ] Create `PlaybackHub` (SignalR)
- [ ] Create `PlaybackCoordinatorService` (in-memory device registry)
- [ ] Add DTOs: `PlaybackState`, `PlaybackCommand`
- [ ] Configure SignalR in `Program.cs`
- [ ] Add `/hubs/playback` endpoint

### Frontend
- [ ] Install `@microsoft/signalr` package
- [ ] Create `playback-hub.ts` client
- [ ] Add Jotai atoms for remote playback state (including derived atoms)
- [ ] Update all UI components to read from derived atoms instead of local player state
- [ ] Implement control handlers that route to local player OR remote commands based on mode
- [ ] Implement `executePlaybackCommand()` handler
- [ ] Create `DevicePickerModal` component
- [ ] Create `RemoteControlBanner` component (prominent indicator)
- [ ] Add device picker button to player UI
- [ ] Update player UI to show `RemoteControlBanner` when in remote mode
- [ ] Implement state sync (debounced updates from player backend events)
- [ ] Handle reconnection logic
- [ ] Add playback active notification

## Testing Scenarios

1. **Basic remote control**: Control playback on Device B from Device A
2. **Disconnect handling**: Disconnect Device B while being controlled from A
3. **Simultaneous control**: Two devices try to control the same device
4. **Takeover**: Device A playing, Device B takes over
5. **Multiple devices**: User has 3+ devices online simultaneously
6. **Network issues**: Reconnect after temporary disconnection
