# Server Configuration System - Final Design

## Overview

Coral will use Microsoft.Extensions.Configuration for managing settings with automatic environment variable binding. Configuration includes JWT authentication, database connection, and file paths.

## Configuration Structure

### JSON Configuration Model

```csharp
// src/Coral.Configuration/Models/ServerConfiguration.cs
namespace Coral.Configuration.Models;

public class ServerConfiguration
{
    public const int CurrentVersion = 1;

    public int ConfigVersion { get; set; } = CurrentVersion;
    public JwtSettings Jwt { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public PathSettings Paths { get; set; } = new();
}

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    // Note: Access token expiry is fixed at 1 day in code (not configurable)
}

public class DatabaseSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "admin";
    public string Database { get; set; } = "coral2";

    [JsonIgnore]
    public string ConnectionString =>
        $"Host={Host};Port={Port};Username={Username};Password={Password};Database={Database}";
}

public class PathSettings
{
    public string Data { get; set; } = GetDefaultDataDirectory();

    // All subdirectories derived from Data directory
    [JsonIgnore]
    public string Thumbnails => Path.Combine(Data, "Thumbnails");

    [JsonIgnore]
    public string ExtractedArtwork => Path.Combine(Data, "Extracted Artwork");

    [JsonIgnore]
    public string Plugins => Path.Combine(Data, "Plugins");

    [JsonIgnore]
    public string Models => Path.Combine(Data, "Models");

    // HLS stays in temp directory (separate from user data)
    [JsonIgnore]
    public string HlsDirectory => Path.Combine(Path.GetTempPath(), "CoralHLS");

    private static string GetDefaultDataDirectory()
    {
        // Docker: use /data, Normal: use LocalApplicationData/Coral
        return File.Exists("/.dockerenv")
            ? "/data"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coral");
    }
}
```

### Example config.json (Windows)

```json
{
  "configVersion": 1,
  "jwt": {
    "secret": "A7F3E92D8B4C1A5E6D9F8B3C2E1A4D7B8C9E2F5A6D8B4C7E9F1A3D5B8C4E7F9A2"
  },
  "database": {
    "host": "localhost",
    "port": 5432,
    "username": "postgres",
    "password": "admin",
    "database": "coral2"
  },
  "paths": {
    "data": "C:\\Users\\User\\AppData\\Local\\Coral"
  }
}
```

### Example config.json (Docker)

```json
{
  "configVersion": 1,
  "jwt": {
    "secret": "B8E4F93C9D5B2A6F7E0D9C4B3A2F1D8E7C0B9A8F6D5C4E3B2A1F0D9C8E7B6A5"
  },
  "database": {
    "host": "coral-db",
    "port": 5432,
    "username": "coral",
    "password": "secure_password",
    "database": "coral"
  },
  "paths": {
    "data": "/data"
  }
}
```

## Configuration Migration System

Coral uses a version-based migration system to safely upgrade configurations when the schema changes.

### Migration Interface

```csharp
// src/Coral.Configuration/Migrations/IConfigurationMigrator.cs
using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrations;

public interface IConfigurationMigrator
{
    int TargetVersion { get; }      // Which version this migration applies to
    int DestinationVersion { get; } // What version it upgrades to
    void Migrate(JsonNode config);  // Perform the migration
}
```

### Migration Runner

```csharp
// src/Coral.Configuration/Migrations/ConfigMigration.cs
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrations;

public class ConfigMigration
{
    private readonly List<IConfigurationMigrator> _migrators;

    public ConfigMigration(int fromVersion)
    {
        // Auto-discover all migrators via reflection
        _migrators = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IConfigurationMigrator).IsAssignableFrom(t) && !t.IsInterface)
            .Select(t => (IConfigurationMigrator)Activator.CreateInstance(t)!)
            .Where(m => m.TargetVersion >= fromVersion)
            .OrderBy(m => m.TargetVersion)
            .ToList();
    }

    public void Migrate(string configFilePath)
    {
        foreach (var migrator in _migrators)
        {
            var configBytes = File.ReadAllBytes(configFilePath);
            var config = JsonSerializer.Deserialize<JsonNode>(configBytes)!;

            // Create backup before migration
            var backupPath = configFilePath.Replace(".json", $".v{migrator.TargetVersion}.json.bak");
            File.Copy(configFilePath, backupPath, overwrite: true);

            Console.WriteLine($"Migrating configuration: v{migrator.TargetVersion} → v{migrator.DestinationVersion}");

            try
            {
                migrator.Migrate(config);

                // Write updated config
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configFilePath, json);

                Console.WriteLine($"✓ Migration to v{migrator.DestinationVersion} completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Migration to v{migrator.DestinationVersion} failed: {ex.Message}");
                Console.WriteLine($"Backup saved at: {backupPath}");
                Console.WriteLine("Please restore from backup or delete config to regenerate.");
                throw;
            }
        }
    }
}
```

### Example Migrator

```csharp
// src/Coral.Configuration/Migrations/ConfigMigrator1.cs
using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrations;

/// <summary>
/// Example: Migrates from v1 to v2 (when we add new settings in the future)
/// </summary>
public class ConfigMigrator1 : IConfigurationMigrator
{
    public int TargetVersion => 1;
    public int DestinationVersion => 2;

    public void Migrate(JsonNode config)
    {
        // Example: Add new setting
        // config["newSection"] = new JsonObject
        // {
        //     ["newSetting"] = "defaultValue"
        // };

        // Update version
        config["configVersion"] = DestinationVersion;
    }
}
```

## Configuration Manager

```csharp
// src/Coral.Configuration/ConfigurationManager.cs
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Coral.Configuration.Models;
using Coral.Configuration.Migrations;

namespace Coral.Configuration;

public static class ConfigurationManager
{
    private const string ApplicationName = "Coral";
    private const string ConfigFileName = "config.json";

    // Docker detection via /.dockerenv file
    private static bool RunningInDocker => File.Exists("/.dockerenv");

    private static string ConfigurationDirectory =>
        RunningInDocker
            ? "/config"
            : Path.Combine(GetConfigDirectory(), ApplicationName);

    public static string ConfigurationFilePath =>
        Path.Combine(ConfigurationDirectory, ConfigFileName);

    private static string GetConfigDirectory()
    {
        var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrEmpty(folderPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : folderPath;
    }

    public static ServerConfiguration LoadConfiguration()
    {
        EnsureDirectoriesExist();

        // Create config file with defaults if it doesn't exist
        if (!File.Exists(ConfigurationFilePath))
        {
            var defaultConfig = CreateDefaultConfiguration();
            SaveConfiguration(defaultConfig);
            Console.WriteLine("Created default configuration with auto-generated JWT secret.");
        }
        else
        {
            // Check if migration is needed
            CheckAndRunMigrations();
        }

        // Load configuration using Microsoft.Extensions.Configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationFilePath, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "CORAL_")
            .Build();

        var config = new ServerConfiguration();
        configuration.Bind(config);

        return config;
    }

    private static void CheckAndRunMigrations()
    {
        try
        {
            // Read current config version
            var configBytes = File.ReadAllBytes(ConfigurationFilePath);
            var configNode = JsonSerializer.Deserialize<JsonNode>(configBytes);
            var currentVersion = configNode?["configVersion"]?.GetValue<int>() ?? 0;

            if (currentVersion < ServerConfiguration.CurrentVersion)
            {
                Console.WriteLine($"Configuration needs migration: v{currentVersion} → v{ServerConfiguration.CurrentVersion}");
                var migration = new ConfigMigration(currentVersion);
                migration.Migrate(ConfigurationFilePath);
                Console.WriteLine("Configuration migration completed successfully.");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error reading configuration version: {ex.Message}");
            Console.WriteLine("Your configuration file may be corrupt. Please delete it to regenerate.");
            throw;
        }
    }

    public static void SaveConfiguration(ServerConfiguration config)
    {
        EnsureDirectoriesExist();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(ConfigurationFilePath, json);

        Console.WriteLine($"Configuration saved to: {ConfigurationFilePath}");
    }

    private static ServerConfiguration CreateDefaultConfiguration()
    {
        return new ServerConfiguration
        {
            ConfigVersion = ServerConfiguration.CurrentVersion,
            Jwt = new JwtSettings
            {
                Secret = GenerateSecureRandomSecret()
            },
            Database = new DatabaseSettings(),
            Paths = new PathSettings()
        };
    }

    private static string GenerateSecureRandomSecret()
    {
        // Generate a 64-character random hex string (256 bits)
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes);
    }

    private static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(ConfigurationDirectory);
    }

    public static void EnsureDataDirectoriesExist(PathSettings paths)
    {
        Directory.CreateDirectory(paths.Data);
        Directory.CreateDirectory(paths.Thumbnails);
        Directory.CreateDirectory(paths.ExtractedArtwork);
        Directory.CreateDirectory(paths.Plugins);
        Directory.CreateDirectory(paths.Models);
        Directory.CreateDirectory(paths.HlsDirectory);
    }
}
```

## Updated ApplicationConfiguration (Backwards Compatibility)

```csharp
// src/Coral.Configuration/Configuration.cs (updated)
namespace Coral.Configuration;

public static class ApplicationConfiguration
{
    private static ServerConfiguration? _config;

    private static ServerConfiguration Config
    {
        get
        {
            if (_config == null)
            {
                _config = ConfigurationManager.LoadConfiguration();
                ConfigurationManager.EnsureDataDirectoriesExist(_config.Paths);
            }
            return _config;
        }
    }

    // Backwards compatibility properties
    public static string HLSDirectory => Config.Paths.HlsDirectory;
    public static string AppData => Config.Paths.Data;
    public static string Thumbnails => Config.Paths.Thumbnails;
    public static string ExtractedArtwork => Config.Paths.ExtractedArtwork;
    public static string Plugins => Config.Paths.Plugins;
    public static string Models => Config.Paths.Models;

    public static void EnsureDirectoriesAreCreated()
    {
        ConfigurationManager.EnsureDataDirectoriesExist(Config.Paths);
    }
}
```

## Integration with ASP.NET Core

```csharp
// src/Coral.Api/Program.cs
using Coral.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load Coral configuration
var config = ConfigurationManager.LoadConfiguration();

// Add to DI container
builder.Services.AddSingleton(config);

// Configure database
builder.Services.AddDbContext<CoralDbContext>(options =>
    options.UseNpgsql(config.Database.ConnectionString, opt => opt.UseVector()));

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(config.Jwt.Secret)
            ),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ... other services

var app = builder.Build();

// Ensure all data directories exist
ConfigurationManager.EnsureDataDirectoriesExist(config.Paths);

app.UseAuthentication();
app.UseAuthorization();

app.Run();
```

## Update CoralDbContext

```csharp
// src/Coral.Database/CoralDbContext.cs
public class CoralDbContext : DbContext
{
    public DbSet<Artwork> Artworks { get; set; }
    public DbSet<Track> Tracks { get; set; } = null!;
    // ... other DbSets

    public CoralDbContext(DbContextOptions<CoralDbContext> options)
        : base(options)
    {
    }

    // Remove OnConfiguring with hardcoded connection string
    // Connection string now comes from DI configuration

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<TrackEmbedding>()
            .HasIndex(i => i.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);
    }
}
```

## Environment Variables

Environment variables use the .NET convention with double underscores for hierarchy.

**Format:** `CORAL_{Section}__{Property}`

### Database Settings
- `CORAL_Database__Host` - PostgreSQL host (default: `localhost`)
- `CORAL_Database__Port` - PostgreSQL port (default: `5432`)
- `CORAL_Database__Username` - Database username (default: `postgres`)
- `CORAL_Database__Password` - Database password (default: `admin`)
- `CORAL_Database__Database` - Database name (default: `coral2`)

### Path Settings
- `CORAL_Paths__Data` - Base data directory
  - Default (normal): `LocalApplicationData/Coral`
  - Default (Docker): `/data`

**Note:** JWT secret is NOT configurable via environment variables. It is auto-generated on first run and stored in config.json.

## Docker Support

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app
COPY publish/ .

# Create config and data directories
RUN mkdir -p /config /data /tmp/hls

EXPOSE 7214

ENTRYPOINT ["dotnet", "Coral.Api.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'

services:
  coral-api:
    image: coral:latest
    ports:
      - "7214:7214"
    environment:
      # Database connection
      - CORAL_Database__Host=coral-db
      - CORAL_Database__Port=5432
      - CORAL_Database__Username=coral
      - CORAL_Database__Password=secure_password_here
      - CORAL_Database__Database=coral

      # Optional: Override data directory (defaults to /data in Docker)
      # - CORAL_Paths__Data=/data
    volumes:
      - coral-config:/config      # Configuration file (JWT secret stored here)
      - coral-data:/data          # User data (thumbnails, plugins, etc.)
      - ./music:/music            # Music library
    depends_on:
      - coral-db

  coral-db:
    image: pgvector/pgvector:pg16
    environment:
      - POSTGRES_USER=coral
      - POSTGRES_PASSWORD=secure_password_here
      - POSTGRES_DB=coral
    volumes:
      - coral-db-data:/var/lib/postgresql/data

volumes:
  coral-config:
  coral-data:
  coral-db-data:
```

**Important:** The `coral-config` volume must be persistent. The JWT secret is generated once on first run and stored in `/config/config.json`. If you delete this volume, a new JWT secret will be generated and all existing refresh tokens will be invalidated.

## Implementation Checklist

### Phase 1: Core Models
- [ ] Create `src/Coral.Configuration/Models/ServerConfiguration.cs`
- [ ] Create `JwtSettings`, `DatabaseSettings`, `PathSettings` classes
- [ ] Add `[JsonIgnore]` attributes to derived properties
- [ ] Implement Docker detection in `PathSettings.GetDefaultDataDirectory()`

### Phase 2: Migration System
- [ ] Create `src/Coral.Configuration/Migrations/IConfigurationMigrator.cs` interface
- [ ] Create `src/Coral.Configuration/Migrations/ConfigMigration.cs` runner
- [ ] Create `src/Coral.Configuration/Migrations/ConfigMigrator1.cs` example (can be empty/commented)
- [ ] Implement reflection-based migrator discovery
- [ ] Implement backup creation before migrations
- [ ] Implement version checking in `ConfigurationManager`

### Phase 3: Configuration Manager
- [ ] Create `src/Coral.Configuration/ConfigurationManager.cs`
- [ ] Use `Microsoft.Extensions.Configuration` with JSON + environment variables
- [ ] Implement `LoadConfiguration()` using ConfigurationBuilder
- [ ] Implement `CheckAndRunMigrations()` to run migrations before loading
- [ ] Implement `SaveConfiguration()` with JSON serialization
- [ ] Implement `CreateDefaultConfiguration()` with JWT secret generation
- [ ] Implement Docker detection via `/.dockerenv`
- [ ] Add `Microsoft.Extensions.Configuration.Json` NuGet package
- [ ] Add `Microsoft.Extensions.Configuration.EnvironmentVariables` NuGet package

### Phase 4: Update Existing Code
- [ ] Update `ApplicationConfiguration.cs` to use new `ConfigurationManager`
- [ ] Maintain backwards compatibility via static properties
- [ ] Update `Program.cs` to load configuration
- [ ] Inject `ServerConfiguration` into DI container
- [ ] Update `CoralDbContext.cs` to use DI-provided connection string
- [ ] Remove hardcoded connection string from `OnConfiguring()`

### Phase 5: JWT Integration
- [ ] Update JWT authentication setup in `Program.cs`
- [ ] Use `config.Jwt.Secret` for signing key
- [ ] Hardcode access token expiry to 1 day

### Phase 6: Testing
- [ ] Test config file generation on first run (JWT secret auto-generated)
- [ ] Test loading existing config file
- [ ] Test environment variable overrides (e.g., `CORAL_Database__Host`)
- [ ] Test Docker detection (`/.dockerenv`)
- [ ] Verify Docker defaults to `/data` directory
- [ ] Test database connection with config
- [ ] Test JWT authentication with config secret
- [ ] Test hierarchical env vars (double underscore)
- [ ] Test migration system (create old config, verify migration runs)
- [ ] Test backup creation during migration
- [ ] Test migration failure handling

### Phase 7: Documentation
- [ ] Document environment variable format (`CORAL_{Section}__{Property}`)
- [ ] Create Docker deployment guide
- [ ] Update README with configuration instructions
- [ ] Document JWT secret persistence in Docker volumes

## File Locations

### Development (Windows)
- **Config:** `%APPDATA%\Coral\config.json`
- **Data:** `%LOCALAPPDATA%\Coral\`
- **HLS:** `%TEMP%\CoralHLS\`

### Development (Linux/macOS)
- **Config:** `~/.config/Coral/config.json`
- **Data:** `~/.local/share/Coral/`
- **HLS:** `/tmp/CoralHLS/`

### Docker
- **Config:** `/config/config.json` (persistent volume - contains JWT secret)
- **Data:** `/data/` (persistent volume)
- **HLS:** `/tmp/hls/` (ephemeral)

## Security Notes

1. **JWT Secret:** Auto-generated with 256-bit entropy on first run
2. **JWT Secret Persistence:** Stored in config.json - must persist `/config` volume in Docker
3. **No Environment Override:** JWT secret cannot be set via environment variable for security
4. **File Permissions:** Config file should be readable only by app user (chmod 600 on Linux)
5. **Environment Variables:** Use for database credentials in production/Docker
6. **Docker Volumes:** Always persist `/config` volume to retain JWT secret

## How Migrations Work

When Coral starts, the configuration system:

1. Checks if `config.json` exists
2. If not, creates default configuration with current version
3. If yes, reads the `configVersion` field
4. If `configVersion < CurrentVersion`:
   - Discovers all migrators via reflection
   - Filters migrators where `TargetVersion >= currentVersion`
   - Runs migrators in order (sorted by `TargetVersion`)
   - Creates `.bak` backup before each migration
   - Updates `configVersion` field
5. Loads configuration with environment variable overrides

### Example Migration Scenario

**User has config v1, Coral updates to v3:**

```
Config v1 exists
├─ Backup: config.v1.json.bak
├─ Run ConfigMigrator1: v1 → v2
├─ Backup: config.v2.json.bak
├─ Run ConfigMigrator2: v2 → v3
└─ Load final config v3
```

### Adding New Migrations

When adding new configuration fields in future versions:

1. Increment `ServerConfiguration.CurrentVersion`
2. Create new migrator class (e.g., `ConfigMigrator2.cs`)
3. Set `TargetVersion` to old version, `DestinationVersion` to new version
4. Implement `Migrate(JsonNode config)` to add new fields with defaults
5. Auto-discovery handles the rest

## Migration Path

1. First run generates `config.json` with defaults
2. JWT secret automatically generated and saved
3. Existing `ApplicationConfiguration` static class remains functional (backwards compatible)
4. Database connection moves from hardcoded `CoralDbContext` to config file
5. Docker deployments automatically use `/data` as default data directory
6. Environment variables override config file values automatically (via Microsoft.Extensions.Configuration)
7. Configuration migrations run automatically on startup
8. No breaking changes for existing deployments
