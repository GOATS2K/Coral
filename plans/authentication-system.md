# Authentication & User System Architecture

## Overview

Coral needs a user authentication system to support:
- Multi-user access control
- Per-device playback preferences
- Session management
- Admin-only library management

## User Authentication (Username/Password + JWT)

### Requirements

- Username/password authentication with BCrypt password hashing
- JWT access tokens (1 day expiry)
- Refresh tokens (no expiry, stored in database, revoked on logout)
- Automatic token refresh (transparent to user)
- Role-based access: Admin (manage libraries, users) and User (browse/listen)
- Admin-only user registration
- First-time setup creates default admin user (username: admin, password: admin)

### Authentication Exemptions

Login is NOT required when:
1. **Single user mode**: Only one user exists in the system (typical self-hosted scenario)
2. **Local network access**: Request originates from LAN/localhost addresses:
   - Localhost: `127.0.0.1`, `::1`
   - Private IP ranges: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`
   - Link-local: `169.254.0.0/16`

When exempted, the server automatically issues a JWT token for the single user (or admin).

### Authentication Flow

```typescript
// Frontend: On app launch, check if authentication is required
const response = await fetch('/api/auth/required');
const data = await response.json();

if (data.required) {
  // Show login screen
  navigation.navigate('Login');
} else {
  // Backend returns a JWT token automatically
  await AsyncStorage.setItem('auth_token', data.accessToken);
  await AsyncStorage.setItem('refresh_token', data.refreshToken);
  navigation.navigate('Home');
}
```

### Backend Endpoint

```csharp
[HttpGet("auth/required")]
public async Task<ActionResult> CheckAuthRequired()
{
    var isSingleUser = await _userService.IsSingleUserMode();
    var isLocalNetwork = IsLocalNetworkAddress(Request.HttpContext.Connection.RemoteIpAddress);

    if (isSingleUser || isLocalNetwork)
    {
        // Auto-login: generate tokens for the single user/admin
        var user = await _userService.GetSingleUserOrAdmin();
        var accessToken = _authService.GenerateAccessToken(user);
        var refreshToken = await _authService.GenerateRefreshToken(user.Id);

        return Ok(new {
            required = false,
            accessToken = accessToken,
            refreshToken = refreshToken,
            expiresIn = 86400, // 1 day in seconds
            user = new { user.Username, user.Email, user.Role }
        });
    }

    return Ok(new { required = true });
}
```

## Token Refresh Mechanism

Users should never see "session expired" errors. The frontend automatically refreshes tokens before they expire.

### Frontend Implementation

```typescript
// Frontend: Token refresh logic
let refreshPromise: Promise<void> | null = null;

async function refreshAccessToken() {
  // Prevent multiple simultaneous refresh attempts
  if (refreshPromise) return refreshPromise;

  refreshPromise = (async () => {
    const refreshToken = await AsyncStorage.getItem('refresh_token');

    const response = await fetch('/api/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken })
    });

    if (!response.ok) {
      // Refresh token was revoked or invalid - user must re-login
      await AsyncStorage.multiRemove(['auth_token', 'refresh_token']);
      navigation.navigate('Login');
      throw new Error('Refresh failed');
    }

    const data = await response.json();
    await AsyncStorage.setItem('auth_token', data.accessToken);
    // Note: refresh token stays the same, no need to update it

    // Schedule next refresh before token expires (e.g., at 80% of lifetime)
    scheduleTokenRefresh(data.expiresIn);
  })();

  try {
    await refreshPromise;
  } finally {
    refreshPromise = null;
  }
}

// Automatically refresh tokens before they expire
function scheduleTokenRefresh(expiresIn: number) {
  const refreshAt = (expiresIn * 0.8) * 1000; // Refresh at 80% of lifetime (~19 hours)
  setTimeout(() => refreshAccessToken(), refreshAt);
}

// API client: Automatically retry failed requests with refreshed token
async function apiRequest(url: string, options: RequestInit = {}) {
  const token = await AsyncStorage.getItem('auth_token');

  const response = await fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      'Authorization': `Bearer ${token}`
    }
  });

  // If 401 Unauthorized, try refreshing token and retry once
  if (response.status === 401) {
    await refreshAccessToken();

    const newToken = await AsyncStorage.getItem('auth_token');
    return fetch(url, {
      ...options,
      headers: {
        ...options.headers,
        'Authorization': `Bearer ${newToken}`
      }
    });
  }

  return response;
}
```

### Benefits

- Zero-friction experience for personal/local use
- Users never have to log in again (refresh tokens don't expire)
- Automatic token refresh happens transparently in background
- Simple implementation - refresh tokens stay valid until explicit revocation
- No client/server sync issues - refresh token never changes
- Standard JWT flow works everywhere (no special middleware)
- Security preserved when exposing externally with multiple users
- Users can manage active sessions and revoke access to lost devices
- No configuration needed - automatically adapts to setup

## Device Registration & Identification

Each client generates a persistent device ID (UUID) on first launch:
- Stored in localStorage (web) or AsyncStorage (mobile)
- Sent to backend on first API call to register device
- Backend tracks: device name, type (Desktop/Mobile/Tablet/TV), platform (web/ios/android)
- Users can manage their devices (rename, delete) via settings

**Note:** Device registration still happens even when auth is exempted - this allows per-device preferences to work in single-user/local network scenarios.

## Per-Device Playback Preferences

Users configure playback quality per-device (phone vs desktop have different needs):

### Quality Settings

1. **Direct Play** (default): Stream files as-is, fallback to Direct Stream if codec unsupported
2. **Direct Stream**: Always remux to compatible container (no transcoding)
3. **Lossless Transcode**: Transcode lossless formats → FLAC, remux lossy formats only
4. **Lossy Transcode**: Allow lossy→lossy transcoding (requires user consent warning)

### Bandwidth Limits

- Max bitrate cap (kbps, null = unlimited)
- Separate cellular bitrate limit (default: 320 kbps)
- Enable/disable cellular limit toggle

### Other Settings

- Gapless playback (default: true)
- Crossfade duration (default: 0 seconds)

## Database Models

### New Tables

- `User`: Username, email, password hash, role, timestamps
- `RefreshToken`: Token ID (jti from JWT), user FK, device ID, created at, revoked at (nullable)
- `Device`: Device ID, name, type, platform, user FK, timestamps
- `DevicePreferences`: Quality setting, bitrate limits, playback settings, device FK

### Updated Tables

- `FavoriteTrack`, `FavoriteArtist`, `FavoriteAlbum`, `Playlist`: Add `UserId` FK

### User Model

```csharp
namespace Coral.Database.Models;

public class User : BaseTable
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; } = UserRole.User;

    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<Device> Devices { get; set; } = new();
    public List<RefreshToken> RefreshTokens { get; set; } = new();
    public List<Playlist> Playlists { get; set; } = new();
    public List<FavoriteTrack> FavoriteTracks { get; set; } = new();
    public List<FavoriteArtist> FavoriteArtists { get; set; } = new();
    public List<FavoriteAlbum> FavoriteAlbums { get; set; } = new();
}

public enum UserRole
{
    User = 0,
    Admin = 1
}
```

### RefreshToken Model

```csharp
public class RefreshToken : BaseTable
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid TokenId { get; set; }  // The jti (JWT ID) from the token
    public string? DeviceId { get; set; }  // Optional: track which device this token is for

    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }  // null = active, non-null = revoked

    public bool IsActive => RevokedAt == null;
}
```

**Note:**
- Refresh tokens never expire. Users remain logged in until they explicitly log out or an admin revokes their token.
- Each token includes a unique `jti` (JWT ID) claim that's stored in the database for validation.
- Create a unique index on `TokenId` for fast lookups during token refresh.

**Entity Framework Configuration:**
```csharp
// In CoralDbContext.OnModelCreating()
modelBuilder.Entity<RefreshToken>()
    .HasIndex(t => t.TokenId)
    .IsUnique();
```

### Token Refresh Implementation

Refresh tokens remain valid indefinitely until explicitly revoked (logout or admin action). This keeps the implementation simple and resilient.

**Token Structure:**
- Both access and refresh tokens include a `jti` (JWT ID) claim with a unique UUID
- Access tokens: `jti` used for audit trails (optional)
- Refresh tokens: `jti` stored in database for validation and revocation

**Benefits of using `jti`:**
- Standard JWT claim designed for token identification
- No need to hash/store entire token - just store the UUID
- Faster database lookups (indexed UUID vs string comparison)
- Cleaner code - JWT library handles claim extraction

```csharp
// Backend: Generate access token with jti
public string GenerateAccessToken(User user)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"]);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("token_type", "access")
        }),
        Expires = DateTime.UtcNow.AddDays(1),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature
        )
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}

// Backend: Generate refresh token with jti
public async Task<string> GenerateRefreshToken(Guid userId, string? deviceId = null)
{
    var tokenId = Guid.NewGuid();

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"]);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, tokenId.ToString()),
            new Claim("token_type", "refresh")
        }),
        Expires = null, // Refresh tokens don't expire
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature
        )
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);

    // Store the jti in database
    await _context.RefreshTokens.AddAsync(new RefreshToken
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenId = tokenId,
        DeviceId = deviceId,
        CreatedAt = DateTime.UtcNow
    });

    await _context.SaveChangesAsync();

    return tokenHandler.WriteToken(token);
}

// Backend: Token refresh endpoint
[HttpPost("auth/refresh")]
public async Task<ActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
{
    // Parse token and extract jti
    var tokenHandler = new JwtSecurityTokenHandler();
    var token = tokenHandler.ReadJwtToken(request.RefreshToken);
    var jtiClaim = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);

    if (jtiClaim == null || !Guid.TryParse(jtiClaim.Value, out var tokenId))
    {
        return Unauthorized(new { error = "Invalid token format" });
    }

    // Look up token in database by jti
    var storedToken = await _context.RefreshTokens
        .Include(t => t.User)
        .FirstOrDefaultAsync(t => t.TokenId == tokenId);

    if (storedToken == null || !storedToken.IsActive)
    {
        return Unauthorized(new { error = "Invalid or revoked refresh token" });
    }

    // Generate new access token (refresh token stays the same)
    var user = storedToken.User;
    var newAccessToken = _authService.GenerateAccessToken(user);

    return Ok(new {
        accessToken = newAccessToken,
        expiresIn = 86400 // 1 day
    });
}
```

**Note:** Refresh tokens are only revoked on explicit logout or admin action. This simplifies the implementation and avoids sync issues between client and server.

### Device Model

```csharp
namespace Coral.Database.Models;

public class Device : BaseTable
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Device identification
    public string DeviceId { get; set; } = null!;  // Client-generated UUID
    public string DeviceName { get; set; } = null!; // "Chrome on Windows", "iPhone 15"
    public DeviceType Type { get; set; }
    public string Platform { get; set; } = null!;   // "web", "ios", "android"

    // Timestamps
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }

    // Playback preferences
    public DevicePreferences? Preferences { get; set; }
}

public enum DeviceType
{
    Desktop = 0,
    Mobile = 1,
    Tablet = 2,
    TV = 3
}
```

### DevicePreferences Model

```csharp
namespace Coral.Database.Models;

public class DevicePreferences : BaseTable
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    // Playback quality
    public PlaybackQuality Quality { get; set; } = PlaybackQuality.DirectPlay;

    // Bandwidth limits
    public int? MaxBitrate { get; set; }  // kbps, null = unlimited
    public bool LimitOnCellular { get; set; } = true;
    public int? CellularMaxBitrate { get; set; } = 320; // kbps

    // Playback settings
    public bool GaplessPlayback { get; set; } = true;
    public int CrossfadeDuration { get; set; } = 0; // seconds
}

public enum PlaybackQuality
{
    DirectPlay = 0,       // Stream as-is, fallback to DirectStream
    DirectStream = 1,     // Always remux
    LosslessTranscode = 2, // Transcode lossless→FLAC, remux lossy
    LossyTranscode = 3    // Allow lossy→lossy transcodes
}
```

## API Endpoints

**Note:** All endpoints marked `[Authorize]` are subject to authentication exemptions (single-user mode or LAN access). When exempted, no JWT token is required and requests are auto-authenticated.

### Authentication

- `POST /api/auth/login` - Login (returns access token + refresh token)
- `POST /api/auth/refresh` - Refresh access token using refresh token
- `POST /api/auth/logout` - Revoke refresh token
- `POST /api/auth/register` - Register new user (admin only)
- `GET /api/auth/me` - Get current user info
- `GET /api/auth/required` - Check if authentication is required (returns access + refresh tokens if not required)

### Device Management

- `POST /api/devices/register` - Register/update device
- `GET /api/devices` - List user's devices
- `GET /api/devices/{deviceId}/preferences` - Get device preferences
- `PUT /api/devices/{deviceId}/preferences` - Update device preferences
- `DELETE /api/devices/{id}` - Remove device

### Session Management

- `GET /api/auth/sessions` - List active sessions (devices with active refresh tokens)
- `DELETE /api/auth/sessions/{deviceId}` - Revoke all refresh tokens for a specific device
- `DELETE /api/auth/sessions` - Revoke all refresh tokens (log out from all devices)

### User Management (Admin only)

- `GET /api/users` - List all users
- `DELETE /api/users/{id}` - Delete user

## Services

### IAuthService

```csharp
namespace Coral.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<UserDto> RegisterAsync(RegisterRequest request);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<UserDto?> GetUserByUsernameAsync(string username);
    Task<List<UserDto>> GetAllUsersAsync(); // Admin only
    Task<bool> DeleteUserAsync(Guid userId); // Admin only

    // Token management
    string GenerateAccessToken(User user);
    Task<string> GenerateRefreshToken(Guid userId, string? deviceId = null);
    Task<RefreshToken?> GetRefreshTokenByJti(Guid tokenId);
    Task RevokeRefreshToken(Guid tokenId);
    Task RevokeAllUserTokens(Guid userId);
    Task RevokeDeviceTokens(Guid userId, string deviceId);

    // Password management
    Task<bool> ValidatePasswordAsync(string password, string passwordHash);
}
```

### IDeviceService

```csharp
namespace Coral.Services;

public interface IDeviceService
{
    Task<DeviceDto> RegisterOrUpdateDeviceAsync(Guid userId, RegisterDeviceRequest request);
    Task<List<DeviceDto>> GetUserDevicesAsync(Guid userId);
    Task<DeviceDto?> GetDeviceAsync(Guid userId, string deviceId);
    Task<DevicePreferencesDto> GetDevicePreferencesAsync(Guid userId, string deviceId);
    Task<DevicePreferencesDto> UpdateDevicePreferencesAsync(
        Guid userId,
        string deviceId,
        UpdateDevicePreferencesRequest request
    );
    Task<bool> DeleteDeviceAsync(Guid userId, Guid deviceDbId);
}
```

## DTOs

### Authentication DTOs

```csharp
namespace Coral.Dto.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string Username,
    string Email,
    UserRole Role
);

public record RefreshTokenRequest(string RefreshToken);

public record RegisterRequest(
    string Username,
    string Email,
    string Password
);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    UserRole Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool IsActive
);
```

### Device DTOs

```csharp
namespace Coral.Dto.Devices;

public record RegisterDeviceRequest(
    string DeviceId,
    string DeviceName,
    DeviceType Type,
    string Platform
);

public record DeviceDto(
    Guid Id,
    string DeviceId,
    string DeviceName,
    DeviceType Type,
    string Platform,
    DateTime FirstSeen,
    DateTime LastSeen,
    DevicePreferencesDto? Preferences
);

public record DevicePreferencesDto(
    PlaybackQuality Quality,
    int? MaxBitrate,
    bool LimitOnCellular,
    int? CellularMaxBitrate,
    bool GaplessPlayback,
    int CrossfadeDuration
);

public record UpdateDevicePreferencesRequest(
    PlaybackQuality? Quality,
    int? MaxBitrate,
    bool? LimitOnCellular,
    int? CellularMaxBitrate,
    bool? GaplessPlayback,
    int? CrossfadeDuration
);
```

## Implementation Checklist

### Backend

- [ ] Create database models: User, RefreshToken, Device, DevicePreferences
- [ ] Add UserId FK to FavoriteTrack, FavoriteArtist, FavoriteAlbum, Playlist
- [ ] Create EF Core migration
- [ ] Implement AuthService (BCrypt, JWT access tokens, refresh token generation/validation)
- [ ] Implement DeviceService (device registration, preference management)
- [ ] Implement IP address validation helper (local network detection)
- [ ] Create AuthController (login, refresh, logout, register, get current user, auth/required, sessions)
- [ ] Create DevicesController (register device, manage preferences)
- [ ] Configure JWT authentication in Program.cs
- [ ] Create DatabaseInitializer for default admin user creation
- [ ] Update all existing endpoints to require authentication

### Frontend

- [ ] Check `/api/auth/required` on app launch (stores tokens if not required)
- [ ] Create login screen (only show when auth required)
- [ ] Implement automatic token refresh (proactive + reactive on 401)
- [ ] Implement device registration on app launch
- [ ] Create settings page for playback preferences
- [ ] Create sessions management screen (view/revoke active devices)
- [ ] Implement logout functionality

## Configuration

### appsettings.json

```json
{
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars-for-hs256-algorithm",
    "AccessTokenExpiryDays": 1
  }
}
```

### Program.cs Setup

```csharp
// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Secret"])
            ),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();

// ...

app.UseAuthentication();
app.UseAuthorization();

// Initialize database and create default admin
await DatabaseInitializer.InitializeAsync(app);
```

### DatabaseInitializer

```csharp
namespace Coral.Api.Extensions;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

        await context.Database.MigrateAsync();

        // Create default admin if no users exist
        if (!await context.Users.AnyAsync())
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@coral.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            Console.WriteLine("Created default admin user (username: admin, password: admin)");
            Console.WriteLine("Please change the password after first login!");
        }
    }
}
```
