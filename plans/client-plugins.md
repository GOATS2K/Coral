# Coral Client-Side Plugin System Specification

## Overview

Coral's plugin system is **server-centric**, prioritizing extensibility on the self-hosted server while maintaining App Store compliance for mobile apps:

1. **Server Plugins (Primary)** - C# DLL plugins providing business logic, integrations, and API endpoints
   - Work with ALL clients (web, iOS, Android, Electron)
   - Full flexibility for self-hosted users
   - Configuration UI auto-generated from JSON Schema

2. **Electron Plugins (Desktop Enhancement)** - TypeScript/React Native plugins for desktop-specific features
   - Custom UI components and routes
   - Native integrations (Discord RPC, system tray, media keys, etc.)
   - Only available on Electron (not web/mobile)

3. **No Dynamic Code Loading on Mobile** - iOS/Android apps are App Store compliant
   - All plugin functionality provided by server plugins
   - Configuration UI auto-generated (no custom plugin UI)
   - Consistent experience with web platform

## Architecture Alignment

**Server-Centric Philosophy:**
- **Server Plugins**: The primary extensibility point (C# DLLs)
- **Electron Plugins**: Optional desktop enhancements (TypeScript/React Native)
- **Mobile/Web**: Generic clients that consume server plugin APIs
- **Configuration**: Unified schema-driven UI across all platforms

---

## 1. Plugin Types

Coral supports two types of plugins:

### Server Plugins (Primary)
- **Language**: C# (.NET 9.0)
- **Distribution**: DLL assemblies
- **Scope**: All platforms (web, iOS, Android, Electron)
- **Purpose**: Business logic, integrations, API endpoints
- **UI**: Auto-generated from JSON Schema

### Electron Plugins (Desktop Only)
- **Language**: TypeScript/JavaScript (React Native)
- **Distribution**: npm packages or bundled modules
- **Scope**: Electron desktop app only
- **Purpose**: Custom UI, native integrations (Discord RPC, system tray, etc.)
- **UI**: Full React Native components

## 2. Plugin Manifest Formats

### 2.1 Server Plugin Manifest

Server plugins use a `plugin.json` file for metadata:

```json
{
  "id": "coral-plugin-lastfm",
  "name": "Last.fm Scrobbler",
  "version": "1.0.0",
  "description": "Scrobbles tracks to Last.fm and syncs listening history",
  "author": "Coral Team",
  "license": "MIT",

  "type": "server",

  "compatibility": {
    "coral": "^1.0.0",
    "pluginApi": "^1.0.0"
  },

  "assembly": "Coral.Plugin.LastFM.dll",

  "configurationSchema": {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "type": "object",
    "title": "Last.fm Configuration",
    "properties": {
      "apiKey": {
        "type": "string",
        "title": "API Key",
        "description": "Your Last.fm API key",
        "x-secret": true
      },
      "sharedSecret": {
        "type": "string",
        "title": "Shared Secret",
        "x-secret": true
      },
      "scrobbleThreshold": {
        "type": "number",
        "title": "Scrobble Threshold (%)",
        "default": 50,
        "minimum": 0,
        "maximum": 100
      }
    },
    "required": ["apiKey", "sharedSecret"]
  }
}
```

### 2.2 Electron Plugin Manifest

Electron plugins use a similar manifest:

```json
{
  "id": "coral-plugin-discord-rpc",
  "name": "Discord Rich Presence",
  "version": "1.0.0",
  "description": "Display currently playing track in Discord",
  "author": "Coral Team",
  "license": "MIT",

  "type": "electron",

  "compatibility": {
    "coral": "^1.0.0",
    "electronPluginApi": "^1.0.0"
  },

  "entrypoint": "./dist/index.js",

  "capabilities": [
    "electron:ipc",
    "electron:native",
    "ui:components"
  ],

  "configurationSchema": {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "type": "object",
    "properties": {
      "clientId": {
        "type": "string",
        "title": "Discord Client ID"
      }
    }
  }
}
```

**Key Fields:**
- `type`: "server" or "electron"
- `compatibility`: Version ranges for Coral and plugin API
- `assembly`: (Server only) Path to DLL file
- `entrypoint`: (Electron only) Path to bundled JavaScript
- `capabilities`: (Electron only) Required permissions
- `configurationSchema`: JSON Schema for auto-generated UI

## 3. Version Compatibility System

**Semantic Versioning**

Both Coral and plugins use [Semantic Versioning](https://semver.org/) (MAJOR.MINOR.PATCH):
- **MAJOR**: Breaking changes to plugin API
- **MINOR**: Backwards-compatible new features
- **PATCH**: Backwards-compatible bug fixes

**Version Ranges**

Plugins specify compatible version ranges using npm-style semver ranges:
- `^1.0.0` - Compatible with 1.x.x (>= 1.0.0, < 2.0.0)
- `~1.2.0` - Compatible with 1.2.x (>= 1.2.0, < 1.3.0)
- `>=1.0.0 <2.0.0` - Explicit range
- `*` - Any version (not recommended)

**Coral Version**

Coral exposes its version via constants:

```csharp
// src/Coral.Api/CoralVersion.cs
public static class CoralVersion
{
    public const string Version = "1.0.0";
    public const string PluginApiVersion = "1.0.0"; // Independent of Coral version
}
```

```typescript
// src/coral-app/lib/version.ts
export const CORAL_VERSION = "1.0.0";
export const PLUGIN_API_VERSION = "1.0.0";
```

**Server-Side Compatibility Checking**

```csharp
// src/Coral.PluginHost/PluginLoader.cs

public (Assembly Assembly, IPlugin Plugin)? LoadPluginAssemblies(string assemblyDirectory)
{
    // ... existing loading code ...

    var manifestPath = Path.Combine(assemblyDirectory, "plugin.json");
    if (File.Exists(manifestPath))
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(
            File.ReadAllText(manifestPath));

        // Check server plugin compatibility
        if (manifest.Server?.Compatibility != null)
        {
            if (!IsCompatible(CoralVersion.PluginApiVersion, manifest.Server.Compatibility))
            {
                _logger.LogError(
                    "Plugin {PluginName} requires plugin API {RequiredVersion}, but Coral has {CurrentVersion}. Skipping load.",
                    manifest.Name,
                    manifest.Server.Compatibility,
                    CoralVersion.PluginApiVersion);
                return null;
            }
        }

        // Check Coral version compatibility
        if (manifest.Compatibility?.Coral != null)
        {
            if (!IsCompatible(CoralVersion.Version, manifest.Compatibility.Coral))
            {
                _logger.LogWarning(
                    "Plugin {PluginName} is designed for Coral {RequiredVersion}, but running on {CurrentVersion}. May not function correctly.",
                    manifest.Name,
                    manifest.Compatibility.Coral,
                    CoralVersion.Version);
                // Load anyway, just warn
            }
        }
    }

    // ... continue loading ...
}

private bool IsCompatible(string currentVersion, string requiredRange)
{
    // Use semver library (e.g., SemVersion or NuGet.Versioning)
    var current = SemanticVersion.Parse(currentVersion);
    var range = VersionRange.Parse(requiredRange);
    return range.Satisfies(current);
}
```

**Client-Side Compatibility Checking**

```typescript
// src/coral-app/lib/plugins/PluginLoader.ts

import semver from 'semver';
import { CORAL_VERSION, PLUGIN_API_VERSION } from '@/lib/version';

export class PluginLoader {
  async loadPlugin(pluginId: string): Promise<void> {
    const manifest = await this.readManifest(pluginId);

    // Validate compatibility
    const compatibility = this.validateCompatibility(manifest);
    if (!compatibility.compatible) {
      console.error(
        `Plugin ${manifest.name} is incompatible: ${compatibility.reason}`
      );
      throw new PluginCompatibilityError(
        manifest.name,
        compatibility.reason
      );
    }

    // ... continue loading ...
  }

  private validateCompatibility(manifest: PluginManifest): {
    compatible: boolean;
    reason?: string;
  } {
    // Check plugin API version
    if (manifest.compatibility?.pluginApi) {
      if (!semver.satisfies(PLUGIN_API_VERSION, manifest.compatibility.pluginApi)) {
        return {
          compatible: false,
          reason: `Requires plugin API ${manifest.compatibility.pluginApi}, but Coral has ${PLUGIN_API_VERSION}`
        };
      }
    }

    // Check Coral version (warning only)
    if (manifest.compatibility?.coral) {
      if (!semver.satisfies(CORAL_VERSION, manifest.compatibility.coral)) {
        console.warn(
          `Plugin ${manifest.name} is designed for Coral ${manifest.compatibility.coral}, ` +
          `but running on ${CORAL_VERSION}. May not function correctly.`
        );
        // Don't block loading, just warn
      }
    }

    return { compatible: true };
  }
}
```

**Electron Plugin Compatibility**

```typescript
// src/coral-app/electron/plugin-loader.mjs

import semver from 'semver';

export class ElectronPluginLoader {
  async loadPlugin(pluginId) {
    const manifest = await this.readManifest(pluginId);

    // Check if plugin targets Electron
    if (!manifest.targets.includes('electron')) {
      console.info(`Plugin ${manifest.name} does not target Electron, skipping`);
      return;
    }

    // Validate compatibility
    if (manifest.compatibility?.pluginApi) {
      if (!semver.satisfies(PLUGIN_API_VERSION, manifest.compatibility.pluginApi)) {
        console.error(
          `Plugin ${manifest.name} requires plugin API ${manifest.compatibility.pluginApi}, ` +
          `but Coral has ${PLUGIN_API_VERSION}. Cannot load.`
        );
        return;
      }
    }

    // ... continue loading ...
  }
}
```

**Handling Breaking Changes**

When introducing breaking changes to the plugin API:

1. **Bump Plugin API Version**:
   ```csharp
   // Before: "1.0.0"
   public const string PluginApiVersion = "2.0.0";
   ```

2. **Document Breaking Changes**:
   ```markdown
   # Plugin API v2.0.0 Breaking Changes

   - IPlugin.ConfigureServices() now requires IServiceProvider parameter
   - PluginContext.api has been renamed to PluginContext.apiClient
   - Removed deprecated IPluginService.Initialize() method
   ```

3. **Migration Guide**:
   ```markdown
   # Migrating Plugins from v1 to v2

   ## Update manifest.json
   ```json
   {
     "compatibility": {
       "pluginApi": "^2.0.0"  // Update from ^1.0.0
     }
   }
   ```

   ## Update plugin code
   - Replace `context.api` with `context.apiClient`
   - Add IServiceProvider parameter to ConfigureServices
   ```

4. **Support Period**: Maintain backwards compatibility for at least one major version:
   - v2.0.0 released: Support both v1 and v2 plugins
   - v3.0.0 released: Drop v1 support, maintain v2 support

**User Experience**

When a plugin is incompatible:

```typescript
// src/coral-app/app/(tabs)/settings/plugins.tsx

function PluginCard({ plugin, manifest }) {
  const compatibility = validateCompatibility(manifest);

  if (!compatibility.compatible) {
    return (
      <Card className="mb-4 border-destructive">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <AlertTriangle className="text-destructive" />
            {plugin.name}
          </CardTitle>
          <CardDescription className="text-destructive">
            Incompatible: {compatibility.reason}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Text className="text-sm mb-2">
            Plugin requires: {manifest.compatibility.pluginApi}
          </Text>
          <Text className="text-sm mb-4">
            Coral version: {PLUGIN_API_VERSION}
          </Text>
          <Button variant="outline" onClick={() => checkForUpdates(plugin)}>
            Check for Updates
          </Button>
        </CardContent>
      </Card>
    );
  }

  // ... normal plugin card ...
}
```

---

## 4. Schema-Driven UI Generation (All Platforms)

All clients (web, iOS, Android, Electron) auto-generate plugin configuration UIs from JSON Schema. This provides:
- ✅ **Consistent UX** across all platforms
- ✅ **Zero custom plugin UI** on mobile (App Store compliant)
- ✅ **Automatic validation** based on schema
- ✅ **Type safety** with TypeScript

### 4.1 Configuration Form Generator

```typescript
// src/coral-app/lib/plugins/ConfigurationFormGenerator.tsx

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { jsonSchemaToZod } from 'json-schema-to-zod';

interface ConfigurationFormProps {
  pluginId: string;
  schema: JSONSchema;
  currentConfig?: Record<string, any>;
  onSave: (config: Record<string, any>) => Promise<void>;
}

export function ConfigurationForm({
  pluginId,
  schema,
  currentConfig,
  onSave
}: ConfigurationFormProps) {
  // Convert JSON Schema to Zod for validation
  const zodSchema = jsonSchemaToZod(schema);

  const form = useForm({
    resolver: zodResolver(zodSchema),
    defaultValues: currentConfig ?? getDefaultValues(schema)
  });

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSave)} className="space-y-4">
        <Text className="text-lg font-semibold">{schema.title}</Text>

        {Object.entries(schema.properties).map(([key, prop]) => (
          <FormField
            key={key}
            control={form.control}
            name={key}
            render={({ field }) => (
              <FormItem>
                <FormLabel>{prop.title || key}</FormLabel>
                <FormControl>
                  {renderControl(prop, field)}
                </FormControl>
                {prop.description && (
                  <FormDescription>{prop.description}</FormDescription>
                )}
                <FormMessage />
              </FormItem>
            )}
          />
        ))}

        <Button type="submit">Save Configuration</Button>
      </form>
    </Form>
  );
}

function renderControl(property: JSONSchemaProperty, field: any) {
  // Render appropriate control based on type and hints
  if (property['x-secret']) {
    return <Input type="password" {...field} />;
  }

  if (property.type === 'boolean') {
    return <Switch checked={field.value} onCheckedChange={field.onChange} />;
  }

  if (property.type === 'number') {
    if (property['x-control'] === 'slider') {
      return (
        <Slider
          min={property.minimum}
          max={property.maximum}
          step={property.multipleOf ?? 1}
          value={[field.value]}
          onValueChange={(v) => field.onChange(v[0])}
        />
      );
    }
    return <Input type="number" {...field} />;
  }

  if (property.enum) {
    return (
      <Select value={field.value} onValueChange={field.onChange}>
        <SelectTrigger>
          <SelectValue placeholder="Select..." />
        </SelectTrigger>
        <SelectContent>
          {property.enum.map((option) => (
            <SelectItem key={option} value={option}>
              {option}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }

  // Default: text input
  return <Input type="text" {...field} />;
}

function getDefaultValues(schema: JSONSchema): Record<string, any> {
  const defaults: Record<string, any> = {};

  for (const [key, prop] of Object.entries(schema.properties)) {
    if (prop.default !== undefined) {
      defaults[key] = prop.default;
    } else if (prop.type === 'boolean') {
      defaults[key] = false;
    } else if (prop.type === 'number') {
      defaults[key] = prop.minimum ?? 0;
    } else {
      defaults[key] = '';
    }
  }

  return defaults;
}
```

### 4.2 Plugin Settings Screen

```typescript
// src/coral-app/app/(tabs)/settings/plugins.tsx

export default function PluginsSettingsScreen() {
  // Fetch all plugin schemas from server
  const { data: plugins } = useQuery({
    queryKey: ['plugin-schemas'],
    queryFn: () => apiClient.plugins.getAllPlugins()
  });

  return (
    <ScrollView className="flex-1 p-4">
      <Text className="text-2xl font-bold mb-4">Plugin Configuration</Text>

      {/* Server Plugins */}
      <Text className="text-xl font-semibold mb-2">Server Plugins</Text>
      {plugins?.server.map((plugin) => (
        <ServerPluginCard key={plugin.id} plugin={plugin} />
      ))}

      {/* Electron Plugins (only show on desktop) */}
      {Platform.OS === 'web' && isElectron && (
        <>
          <Text className="text-xl font-semibold mb-2 mt-6">
            Desktop Plugins
          </Text>
          {plugins?.electron.map((plugin) => (
            <ElectronPluginCard key={plugin.id} plugin={plugin} />
          ))}
        </>
      )}
    </ScrollView>
  );
}

function ServerPluginCard({ plugin }) {
  const { data: config, isLoading } = useQuery({
    queryKey: ['plugin-config', plugin.id],
    queryFn: async () => {
      const response = await fetch(
        `/api/plugins/${plugin.configurationEndpoint}`,
        { headers: { Authorization: `Bearer ${getToken()}` } }
      );
      return response.json();
    }
  });

  const updateConfig = useMutation({
    mutationFn: async (newConfig: Record<string, any>) => {
      await fetch(`/api/plugins/${plugin.configurationEndpoint}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${getToken()}`
        },
        body: JSON.stringify(newConfig)
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries(['plugin-config', plugin.id]);
    }
  });

  return (
    <Card className="mb-4">
      <CardHeader>
        <CardTitle>{plugin.name}</CardTitle>
        <CardDescription>{plugin.description}</CardDescription>
      </CardHeader>
      <CardContent>
        {plugin.configurationSchema && (
          <ConfigurationForm
            pluginId={plugin.id}
            schema={plugin.configurationSchema}
            currentConfig={config}
            onSave={(newConfig) => updateConfig.mutateAsync(newConfig)}
          />
        )}
      </CardContent>
    </Card>
  );
}
```

---

## 5. Electron Plugin Architecture

Electron plugins provide full React Native UI customization and native desktop integrations. They are loaded dynamically from the filesystem and can extend the app with custom components, routes, and native features.

### 5.1 Electron Plugin Interface

```typescript
// @coral/plugin-sdk/src/electron.ts

import type { BrowserWindow, IpcMain } from 'electron';

export interface ElectronPlugin {
  readonly id: string;
  readonly name: string;
  readonly version: string;

  onLoad?(context: ElectronPluginContext): void | Promise<void>;
  onUnload?(): void | Promise<void>;

  /** Main process handlers */
  onMainProcessReady?(window: BrowserWindow): void;

  /** IPC handlers */
  ipcHandlers?: Record<string, IpcHandler>;

  /** System integrations */
  registerSystemIntegrations?(): SystemIntegration[];
}

export interface ElectronPluginContext {
  ipcMain: IpcMain;
  storage: PluginStorage;
  config: PluginConfig;

  /** Access to Electron APIs */
  electron: {
    app: Electron.App;
    dialog: Electron.Dialog;
    nativeTheme: Electron.NativeTheme;
    // etc.
  };

  /** Access to native modules */
  native: NativeModuleRegistry;
}

export interface SystemIntegration {
  type: 'tray' | 'menu' | 'notification' | 'protocol';
  config: unknown;
}

export type IpcHandler = (event: IpcMainEvent, ...args: any[]) => any;
```

### 5.2 Electron Plugin Loader

```typescript
// src/coral-app/electron/plugin-loader.mjs

import { app } from 'electron';
import path from 'path';
import fs from 'fs/promises';

export class ElectronPluginLoader {
  constructor(mainWindow) {
    this.mainWindow = mainWindow;
    this.plugins = new Map();
    this.pluginDir = path.join(app.getPath('userData'), 'plugins');
  }

  async loadAll() {
    const pluginDirs = await fs.readdir(this.pluginDir);

    for (const dir of pluginDirs) {
      try {
        await this.loadPlugin(dir);
      } catch (error) {
        console.error(`Failed to load plugin ${dir}:`, error);
      }
    }
  }

  async loadPlugin(pluginId) {
    const manifestPath = path.join(this.pluginDir, pluginId, 'plugin.json');
    const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf-8'));

    if (!manifest.targets.includes('electron')) {
      return; // Skip non-Electron plugins
    }

    const entrypoint = path.join(
      this.pluginDir,
      pluginId,
      manifest.entrypoints.electron
    );

    const module = await import(entrypoint);
    const plugin = new module.default();

    const context = this.createContext(manifest);
    await plugin.onLoad?.(context);

    // Register IPC handlers
    if (plugin.ipcHandlers) {
      for (const [channel, handler] of Object.entries(plugin.ipcHandlers)) {
        const scopedChannel = `plugin:${pluginId}:${channel}`;
        ipcMain.handle(scopedChannel, handler);
      }
    }

    // Initialize system integrations
    if (plugin.registerSystemIntegrations) {
      const integrations = plugin.registerSystemIntegrations();
      this.initializeIntegrations(pluginId, integrations);
    }

    this.plugins.set(pluginId, { plugin, manifest, context });
  }

  createContext(manifest) {
    return {
      ipcMain,
      storage: new ElectronPluginStorage(manifest.id),
      config: new PluginConfig(manifest),
      electron: { app, dialog, nativeTheme, /* ... */ },
      native: new NativeModuleRegistry()
    };
  }
}
```

### 5.3 Example: Discord RPC Plugin

**plugin.json:**
```json
{
  "id": "coral-plugin-discord-rpc",
  "name": "Discord Rich Presence",
  "type": "electron",
  "version": "1.0.0",
  "compatibility": {
    "coral": "^1.0.0",
    "electronPluginApi": "^1.0.0"
  },
  "entrypoint": "./dist/index.js",
  "capabilities": [
    "electron:ipc",
    "electron:native"
  ],
  "configurationSchema": {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "type": "object",
    "title": "Discord Rich Presence Configuration",
    "properties": {
      "clientId": {
        "type": "string",
        "title": "Discord Client ID",
        "description": "Your Discord application client ID",
        "minLength": 1
      },
      "showButtons": {
        "type": "boolean",
        "title": "Show 'Listen on Coral' button",
        "default": true
      }
    },
    "required": ["clientId"]
  }
}
```

**src/electron.ts:**
```typescript
import DiscordRPC from 'discord-rpc';
import type { ElectronPlugin, ElectronPluginContext } from '@coral/plugin-sdk/electron';

export default class DiscordRpcPlugin implements ElectronPlugin {
  id = 'coral-plugin-discord-rpc';
  name = 'Discord Rich Presence';
  version = '1.0.0';

  private rpc?: DiscordRPC.Client;
  private context?: ElectronPluginContext;
  private config?: { clientId: string; showButtons: boolean };

  async onLoad(context: ElectronPluginContext) {
    this.context = context;

    // Configuration managed via schema-driven UI (see plugin.json)
    this.config = await context.config.getAll();

    if (!this.config?.clientId) {
      console.warn('Discord RPC: Client ID not configured');
      return;
    }

    this.rpc = new DiscordRPC.Client({ transport: 'ipc' });
    await this.rpc.login({ clientId: this.config.clientId });
  }

  async onUnload() {
    await this.rpc?.destroy();
  }

  ipcHandlers = {
    'update-presence': async (event, track, state) => {
      const buttons = this.config?.showButtons
        ? [{ label: 'Listen on Coral', url: 'https://coral.example.com' }]
        : undefined;

      await this.rpc?.setActivity({
        details: track.title,
        state: `by ${track.artist}`,
        largeImageKey: 'coral-logo',
        largeImageText: 'Coral Music',
        buttons
      });
    }
  };
}
```

**Configuration UI (auto-generated from schema):**
- Users configure the plugin via the unified settings page
- Form is automatically generated from `configurationSchema` in plugin.json
- Same `ConfigurationForm` component used for server plugins (Section 4)

---

## 6. Plugin Discovery API

The server exposes API endpoints for clients to discover available plugins and their configuration schemas.

### 6.1 Backend API Endpoints

```csharp
// src/Coral.Api/Controllers/PluginsController.cs

[ApiController]
[Route("api/plugins")]
public class PluginsController : ControllerBase
{
    private readonly IPluginContext _pluginContext;

    // GET /api/plugins/server
    // Returns all loaded server plugins with their configuration metadata
    [HttpGet("server")]
    public ActionResult<List<ServerPluginDto>> GetServerPlugins()
    {
        var plugins = _pluginContext.GetLoadedPlugins();
        return plugins.Select(p => new ServerPluginDto
        {
            Id = p.Plugin.Name,
            Name = p.Plugin.Name,
            Description = p.Plugin.Description,
            Version = p.LoadedAssembly.GetName().Version?.ToString(),
            ConfigurationSchema = p.Plugin is IConfigurablePlugin cp
                ? JsonDocument.Parse(cp.GetConfigurationSchema())
                : null,
            ConfigurationEndpoint = p.Plugin is IConfigurablePlugin cp2
                ? $"/api/plugins/{cp2.ConfigurationEndpoint}"
                : null
        }).ToList();
    }

    // GET /api/plugins/electron
    // Returns available Electron plugins (for Electron clients only)
    [HttpGet("electron")]
    public ActionResult<List<ElectronPluginDto>> GetElectronPlugins()
    {
        // Read Electron plugin manifests from disk
        var pluginDir = Path.Combine(ApplicationConfiguration.Plugins, "electron");
        if (!Directory.Exists(pluginDir))
        {
            return Ok(new List<ElectronPluginDto>());
        }

        var plugins = new List<ElectronPluginDto>();
        foreach (var dir in Directory.GetDirectories(pluginDir))
        {
            var manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath)) continue;

            var manifest = JsonSerializer.Deserialize<ElectronPluginManifest>(
                File.ReadAllText(manifestPath));

            plugins.Add(new ElectronPluginDto
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Description = manifest.Description,
                Version = manifest.Version,
                ConfigurationSchema = manifest.ConfigurationSchema
            });
        }

        return Ok(plugins);
    }

    // GET /api/plugins/all-schemas
    // Returns schemas for ALL plugins (server + Electron) for unified settings UI
    [HttpGet("all-schemas")]
    public async Task<ActionResult<PluginSchemasDto>> GetAllPluginSchemas()
    {
        var serverPlugins = GetServerPlugins().Value;
        var electronPlugins = GetElectronPlugins().Value;

        return new PluginSchemasDto
        {
            ServerPlugins = serverPlugins,
            ElectronPlugins = electronPlugins
        };
    }
}
```

**Note:** Plugin configurations are NOT stored in a centralized database. Each plugin manages its own configuration storage (see Section 7).

### 6.2 Client-Side Plugin Discovery

```typescript
// src/coral-app/lib/plugins/usePlugins.ts

export function useAvailablePlugins() {
  const { data: serverPlugins } = useQuery({
    queryKey: ['plugins', 'server'],
    queryFn: () => apiClient.plugins.getServerPlugins()
  });

  const { data: electronPlugins } = useQuery({
    queryKey: ['plugins', 'electron'],
    queryFn: () => apiClient.plugins.getElectronPlugins(),
    enabled: isElectron // Only fetch on Electron
  });

  return {
    serverPlugins: serverPlugins ?? [],
    electronPlugins: electronPlugins ?? [],
    allPlugins: [
      ...(serverPlugins ?? []),
      ...(electronPlugins ?? [])
    ]
  };
}
```

---

## 7. Unified Plugin Configuration System

### Overview

Both server plugins and Electron plugins need to be configurable from Coral's settings UI. This requires:

1. **Schema-driven configuration** - Plugins expose JSON Schema describing their config fields
2. **Plugin-managed storage** - Each plugin manages its own configuration storage (files, database, external services)
3. **Dynamic form generation** - Client UI automatically generates forms from schemas
4. **Hot-reloading** - Server plugins reload configuration without restart via `OnConfigurationUpdated`
5. **Plugin-specific endpoints** - Each plugin specifies where to send config updates

### 5.1 Configuration Schema Format

Plugins use **JSON Schema** to describe their configuration fields:

```csharp
// src/Coral.PluginBase/IConfigurablePlugin.cs

public interface IConfigurablePlugin : IPlugin
{
    /// <summary>
    /// Returns JSON Schema describing the plugin's configuration
    /// </summary>
    string GetConfigurationSchema();

    /// <summary>
    /// The API route for configuration operations (relative to /api/plugins/)
    /// Example: "lastfm/configuration"
    /// GET {endpoint} - retrieves current configuration
    /// POST {endpoint} - updates configuration
    /// </summary>
    string ConfigurationEndpoint { get; }

    /// <summary>
    /// Retrieves current configuration for a user
    /// Plugin is responsible for storage mechanism
    /// </summary>
    Task<JsonDocument?> GetConfiguration(Guid userId);

    /// <summary>
    /// Saves configuration for a user
    /// Plugin is responsible for storage mechanism and validation
    /// </summary>
    Task SaveConfiguration(Guid userId, JsonDocument configuration);

    /// <summary>
    /// Called after configuration is updated (for hot-reloading services)
    /// </summary>
    Task OnConfigurationUpdated(Guid userId, JsonDocument newConfiguration);
}
```

**Example Schema (Last.fm plugin):**

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "title": "Last.fm Configuration",
  "properties": {
    "apiKey": {
      "type": "string",
      "title": "API Key",
      "description": "Your Last.fm API key",
      "minLength": 1,
      "x-secret": true,
      "x-placeholder": "Enter your Last.fm API key"
    },
    "sharedSecret": {
      "type": "string",
      "title": "Shared Secret",
      "description": "Your Last.fm shared secret",
      "minLength": 1,
      "x-secret": true,
      "x-placeholder": "Enter your shared secret"
    },
    "scrobbleThreshold": {
      "type": "number",
      "title": "Scrobble Threshold",
      "description": "Percentage of track played before scrobbling",
      "default": 50,
      "minimum": 0,
      "maximum": 100,
      "x-control": "slider"
    },
    "enabled": {
      "type": "boolean",
      "title": "Enable Scrobbling",
      "description": "Toggle scrobbling on/off",
      "default": true
    }
  },
  "required": ["apiKey", "sharedSecret"]
}
```

**Custom JSON Schema Extensions:**
- `x-secret`: Field contains sensitive data (render as password input)
- `x-placeholder`: Placeholder text for input
- `x-control`: Preferred UI control type (`slider`, `select`, `textarea`, etc.)
- `x-order`: Display order in form

### 5.2 Configuration Storage Philosophy

**Key Principle: Plugins Own Their Configuration**

Each plugin is responsible for storing and managing its own configuration. This provides:
- **Flexibility**: Plugins can use files, database tables, external services, or any storage mechanism
- **Isolation**: Plugin configurations don't pollute the core database schema
- **Plugin autonomy**: Plugin authors control their own data model
- **Simpler core**: No need for generic configuration tables in Coral's database

**Database Changes (Minimal):**

```sql
-- Only add configuration schema to ClientPlugins for UI generation
ALTER TABLE ClientPlugins ADD COLUMN ConfigurationSchema JSONB;

-- Server plugins expose their schema dynamically via IConfigurablePlugin interface
-- No database storage needed for server plugin metadata
```

**How Plugins Store Configuration:**

```csharp
// Example 1: File-based (current Last.fm approach)
public class LastFmPlugin : IConfigurablePlugin
{
    private string ConfigPath => Path.Combine(ApplicationConfiguration.Plugins, "lastfm-config.json");

    public async Task<JsonDocument> GetConfiguration(Guid userId)
    {
        var json = await File.ReadAllTextAsync(ConfigPath);
        return JsonDocument.Parse(json);
    }

    public async Task SaveConfiguration(Guid userId, JsonDocument config)
    {
        await File.WriteAllTextAsync(ConfigPath, config.RootElement.ToString());
    }
}

// Example 2: Plugin-specific database table
public class LastFmPlugin : IConfigurablePlugin
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register custom DbContext for this plugin
        services.AddDbContext<LastFmDbContext>();
    }

    public async Task<JsonDocument> GetConfiguration(Guid userId)
    {
        var config = await _context.LastFmConfigs
            .Where(c => c.UserId == userId)
            .FirstOrDefaultAsync();

        return JsonDocument.Parse(JsonSerializer.Serialize(config));
    }
}

// Example 3: External service (e.g., cloud config)
public class CloudSyncPlugin : IConfigurablePlugin
{
    public async Task<JsonDocument> GetConfiguration(Guid userId)
    {
        var config = await _httpClient.GetAsync($"https://config-api.example.com/users/{userId}/config");
        return await JsonDocument.ParseAsync(await config.Content.ReadAsStreamAsync());
    }
}
```

### 5.3 Updated Server Plugin Interface

**Example: Last.fm Plugin with Configuration**

```csharp
// src/Coral.Plugin.LastFM/LastFmPlugin.cs

public class LastFMPlugin : IConfigurablePlugin
{
    public string Name => "Last.fm";
    public string Description => "A simple track scrobbler.";
    public string ConfigurationEndpoint => "lastfm/configuration";

    private string GetConfigPath(Guid userId) =>
        Path.Combine(ApplicationConfiguration.Plugins, "LastFM", $"config-{userId}.json");

    public string GetConfigurationSchema()
    {
        return JsonSerializer.Serialize(new
        {
            schema = "http://json-schema.org/draft-07/schema#",
            type = "object",
            title = "Last.fm Configuration",
            properties = new
            {
                apiKey = new
                {
                    type = "string",
                    title = "API Key",
                    description = "Your Last.fm API key",
                    minLength = 1,
                    xSecret = true
                },
                sharedSecret = new
                {
                    type = "string",
                    title = "Shared Secret",
                    description = "Your Last.fm shared secret",
                    minLength = 1,
                    xSecret = true
                },
                scrobbleThreshold = new
                {
                    type = "number",
                    title = "Scrobble Threshold",
                    description = "Percentage of track played before scrobbling",
                    @default = 50,
                    minimum = 0,
                    maximum = 100
                }
            },
            required = new[] { "apiKey", "sharedSecret" }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public async Task<JsonDocument?> GetConfiguration(Guid userId)
    {
        var configPath = GetConfigPath(userId);
        if (!File.Exists(configPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(configPath);
        return JsonDocument.Parse(json);
    }

    public async Task SaveConfiguration(Guid userId, JsonDocument configuration)
    {
        var configPath = GetConfigPath(userId);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        await File.WriteAllTextAsync(configPath,
            configuration.RootElement.GetRawText());
    }

    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<ILastFmService, LastFmService>();
        serviceCollection.AddScoped<IPluginService, LastFmService>();
    }

    public async Task OnConfigurationUpdated(Guid userId, JsonDocument newConfiguration)
    {
        // Hot-reload configuration for this user's Last.fm service
        var config = newConfiguration.Deserialize<LastFmConfiguration>();

        // Notify service to reload (implementation depends on service architecture)
        // This could use an event bus, direct service call, etc.
    }
}
```

### 5.4 Configuration API Endpoints

**Core Plugins Controller** (provides metadata and routes to plugin-specific endpoints):

```csharp
// src/Coral.Api/Controllers/PluginsController.cs

[ApiController]
[Route("api/plugins")]
public class PluginsController : ControllerBase
{
    private readonly IPluginContext _pluginContext;

    // GET /api/plugins/server
    // Returns all loaded server plugins with their configuration metadata
    [HttpGet("server")]
    public ActionResult<List<ServerPluginDto>> GetServerPlugins()
    {
        var plugins = _pluginContext.GetLoadedPlugins();
        return plugins.Select(p => new ServerPluginDto
        {
            Id = p.Plugin.Name,
            Name = p.Plugin.Name,
            Description = p.Plugin.Description,
            ConfigurationSchema = p.Plugin is IConfigurablePlugin cp
                ? JsonDocument.Parse(cp.GetConfigurationSchema())
                : null,
            ConfigurationEndpoint = p.Plugin is IConfigurablePlugin cp2
                ? $"/api/plugins/{cp2.ConfigurationEndpoint}"
                : null
        }).ToList();
    }

    // GET /api/plugins/all-schemas
    // Returns schemas for ALL plugins (server + client) for unified settings UI
    [HttpGet("all-schemas")]
    [Authorize]
    public async Task<ActionResult<PluginSchemasDto>> GetAllPluginSchemas()
    {
        var serverPlugins = GetServerPlugins().Value;
        var clientPlugins = await _clientPluginService.GetPluginsWithSchemas();

        return new PluginSchemasDto
        {
            ServerPlugins = serverPlugins,
            ClientPlugins = clientPlugins
        };
    }
}
```

**Plugin-Specific Configuration Controller** (each plugin implements its own):

```csharp
// src/Coral.Plugin.LastFM/LastFmController.cs

[Route("api/plugins/lastfm")]
public class LastFmController : PluginControllerBase
{
    private readonly ILastFmService _lastFmService;
    private readonly IPluginContext _pluginContext;

    // GET /api/plugins/lastfm/configuration
    // Returns current configuration for the authenticated user
    [HttpGet("configuration")]
    [Authorize]
    public async Task<ActionResult<JsonDocument>> GetConfiguration()
    {
        var userId = User.GetUserId();
        var plugin = _pluginContext.GetPlugin<LastFMPlugin>();

        var config = await plugin.GetConfiguration(userId);
        if (config == null)
        {
            return Ok(new { }); // Return empty config if none exists
        }

        return Ok(config);
    }

    // POST /api/plugins/lastfm/configuration
    // Updates configuration for the authenticated user
    [HttpPost("configuration")]
    [Authorize]
    public async Task<ActionResult> SaveConfiguration([FromBody] JsonDocument configuration)
    {
        var userId = User.GetUserId();
        var plugin = _pluginContext.GetPlugin<LastFMPlugin>();

        // Validate against schema (optional, plugin can do its own validation)
        var schema = JsonSchema.FromJsonAsync(plugin.GetConfigurationSchema());
        var errors = schema.Validate(configuration.RootElement.ToString());
        if (errors.Any())
        {
            return BadRequest(new { errors });
        }

        // Plugin handles its own storage
        await plugin.SaveConfiguration(userId, configuration);

        // Trigger hot-reload
        await plugin.OnConfigurationUpdated(userId, configuration);

        return Ok();
    }

    // GET /api/plugins/lastfm/auth-url
    // Additional plugin-specific endpoints for complex workflows
    [HttpGet("auth-url")]
    [Authorize]
    public async Task<ActionResult<string>> GetAuthUrl()
    {
        var url = await _lastFmService.GetAuthorizationUrl();
        return Ok(new { authUrl = url });
    }

    // POST /api/plugins/lastfm/auth-callback
    [HttpPost("auth-callback")]
    [Authorize]
    public async Task<ActionResult> HandleAuthCallback([FromBody] AuthCallbackDto callback)
    {
        var userId = User.GetUserId();
        var session = await _lastFmService.GetSession(callback.Token);

        // Update existing configuration with session key
        var plugin = _pluginContext.GetPlugin<LastFMPlugin>();
        var config = await plugin.GetConfiguration(userId) ?? JsonDocument.Parse("{}");

        // Merge session key into config
        var updatedConfig = MergeSessionKey(config, session.Key);
        await plugin.SaveConfiguration(userId, updatedConfig);

        return Ok();
    }
}
```

### 5.5 Plugin Context Helper Methods

Since plugins manage their own configuration, we need helper methods in `PluginContext` to easily retrieve plugin instances:

```csharp
// src/Coral.PluginHost/PluginContext.cs (additions)

public partial class PluginContext : IPluginContext
{
    // ... existing code ...

    /// <summary>
    /// Gets a loaded plugin by name
    /// </summary>
    public IPlugin? GetPlugin(string pluginName)
    {
        var plugin = _loadedPlugins.Keys
            .FirstOrDefault(p => p.Plugin.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

        return plugin?.Plugin;
    }

    /// <summary>
    /// Gets a loaded plugin by type
    /// </summary>
    public TPlugin? GetPlugin<TPlugin>() where TPlugin : class, IPlugin
    {
        var plugin = _loadedPlugins.Keys
            .FirstOrDefault(p => p.Plugin is TPlugin);

        return plugin?.Plugin as TPlugin;
    }

    /// <summary>
    /// Gets all loaded plugins
    /// </summary>
    public IEnumerable<LoadedPlugin> GetLoadedPlugins()
    {
        return _loadedPlugins.Keys;
    }
}
```

### 5.6 Client-Side Dynamic Form Generation

```typescript
// src/coral-app/lib/plugins/ConfigurationFormGenerator.tsx

import { useForm } from 'react-hook-form';
import { JsonSchema } from './types';

interface ConfigurationFormProps {
  pluginId: string;
  pluginType: 'server' | 'client';
  schema: JsonSchema;
  currentConfig?: Record<string, any>;
  onSave: (config: Record<string, any>) => Promise<void>;
}

export function ConfigurationForm({
  pluginId,
  pluginType,
  schema,
  currentConfig,
  onSave
}: ConfigurationFormProps) {
  const form = useForm({
    defaultValues: currentConfig ?? getDefaultValues(schema)
  });

  const handleSubmit = async (data: Record<string, any>) => {
    await onSave(data);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleSubmit)} className="space-y-4">
        <Text className="text-lg font-semibold">{schema.title}</Text>

        {Object.entries(schema.properties).map(([key, prop]) => (
          <FormField
            key={key}
            control={form.control}
            name={key}
            render={({ field }) => (
              <FormItem>
                <FormLabel>{prop.title}</FormLabel>
                <FormControl>
                  {renderControl(prop, field)}
                </FormControl>
                {prop.description && (
                  <FormDescription>{prop.description}</FormDescription>
                )}
                <FormMessage />
              </FormItem>
            )}
          />
        ))}

        <Button type="submit">Save Configuration</Button>
      </form>
    </Form>
  );
}

function renderControl(property: JsonSchemaProperty, field: any) {
  // Render based on type and x-control hint
  if (property['x-secret'] || property.type === 'password') {
    return <Input type="password" {...field} />;
  }

  if (property['x-control'] === 'slider' && property.type === 'number') {
    return (
      <Slider
        min={property.minimum}
        max={property.maximum}
        step={property.multipleOf ?? 1}
        value={[field.value]}
        onValueChange={(v) => field.onChange(v[0])}
      />
    );
  }

  if (property.type === 'boolean') {
    return <Switch checked={field.value} onCheckedChange={field.onChange} />;
  }

  if (property.enum) {
    return (
      <Select value={field.value} onValueChange={field.onChange}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {property.enum.map((option) => (
            <SelectItem key={option} value={option}>
              {option}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }

  if (property['x-control'] === 'textarea') {
    return <Textarea {...field} />;
  }

  // Default: text input
  return <Input type={property.type} {...field} />;
}

function getDefaultValues(schema: JsonSchema): Record<string, any> {
  const defaults: Record<string, any> = {};

  for (const [key, prop] of Object.entries(schema.properties)) {
    if (prop.default !== undefined) {
      defaults[key] = prop.default;
    } else if (prop.type === 'boolean') {
      defaults[key] = false;
    } else if (prop.type === 'number') {
      defaults[key] = prop.minimum ?? 0;
    } else {
      defaults[key] = '';
    }
  }

  return defaults;
}
```

### 5.7 Settings UI Integration

```typescript
// src/coral-app/app/(tabs)/settings/plugins.tsx

import { useQuery, useMutation } from '@tanstack/react-query';
import { ConfigurationForm } from '@/lib/plugins/ConfigurationFormGenerator';
import { apiClient } from '@/lib/client';

export default function PluginsSettingsScreen() {
  // Fetch all plugin metadata (schemas and endpoints)
  const { data: schemas } = useQuery({
    queryKey: ['plugin-schemas'],
    queryFn: () => apiClient.plugins.getAllSchemas()
  });

  return (
    <ScrollView className="flex-1 p-4">
      <Text className="text-2xl font-bold mb-4">Plugin Configuration</Text>

      {/* Server Plugins */}
      <Text className="text-xl font-semibold mb-2">Server Plugins</Text>
      {schemas?.serverPlugins.map((plugin) => (
        <PluginConfigCard
          key={plugin.id}
          plugin={plugin}
          pluginType="server"
        />
      ))}

      {/* Client Plugins */}
      <Text className="text-xl font-semibold mb-2 mt-6">Client Plugins</Text>
      {schemas?.clientPlugins.map((plugin) => (
        <PluginConfigCard
          key={plugin.id}
          plugin={plugin}
          pluginType="client"
        />
      ))}
    </ScrollView>
  );
}

function PluginConfigCard({ plugin, pluginType }) {
  // Fetch configuration from plugin-specific endpoint
  const { data: config, isLoading } = useQuery({
    queryKey: ['plugin-config', pluginType, plugin.id],
    queryFn: async () => {
      // Each plugin has its own configuration endpoint
      const response = await fetch(plugin.configurationEndpoint, {
        headers: { Authorization: `Bearer ${getToken()}` }
      });
      return response.json();
    },
    enabled: !!plugin.configurationEndpoint
  });

  // Save configuration to plugin-specific endpoint
  const updateConfig = useMutation({
    mutationFn: async (newConfig: Record<string, any>) => {
      const response = await fetch(plugin.configurationEndpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${getToken()}`
        },
        body: JSON.stringify(newConfig)
      });

      if (!response.ok) {
        throw new Error('Failed to save configuration');
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries(['plugin-config', pluginType, plugin.id]);
    }
  });

  return (
    <Card className="mb-4">
      <CardHeader>
        <CardTitle>{plugin.name}</CardTitle>
        <CardDescription>{plugin.description}</CardDescription>
      </CardHeader>
      <CardContent>
        {plugin.configurationSchema && (
          <ConfigurationForm
            pluginId={plugin.id}
            pluginType={pluginType}
            schema={plugin.configurationSchema}
            currentConfig={config}
            isLoading={isLoading}
            onSave={(newConfig) => updateConfig.mutateAsync(newConfig)}
          />
        )}
      </CardContent>
    </Card>
  );
}
```

### 5.8 Configuration Hot-Reloading

Plugins trigger hot-reloading via the `OnConfigurationUpdated` callback:

```csharp
// src/Coral.Plugin.LastFM/LastFmPlugin.cs

public class LastFMPlugin : IConfigurablePlugin
{
    private readonly Dictionary<Guid, ILastFmService> _userServices = new();

    public async Task OnConfigurationUpdated(Guid userId, JsonDocument newConfiguration)
    {
        var config = newConfiguration.Deserialize<LastFmConfiguration>();

        // Option 1: Reload service for this specific user
        if (_userServices.TryGetValue(userId, out var service))
        {
            await service.ReloadConfiguration(config);
        }

        // Option 2: Use event bus to notify services
        _eventBus.Publish(new PluginConfigurationUpdatedEvent
        {
            PluginId = Name,
            UserId = userId,
            Configuration = config
        });

        // Option 3: Reinitialize plugin-wide resources (if needed)
        await ReinitializeSharedResourcesAsync();
    }
}

// src/Coral.Plugin.LastFM/LastFmService.cs

public class LastFmService : ILastFmService
{
    private LastFmConfiguration? _config;
    private readonly Guid _userId;

    public LastFmService(IHttpContextAccessor httpContextAccessor)
    {
        _userId = httpContextAccessor.HttpContext?.User.GetUserId() ?? Guid.Empty;

        // Subscribe to configuration change events
        _eventBus.Subscribe<PluginConfigurationUpdatedEvent>(OnConfigChanged);
    }

    private async Task OnConfigChanged(PluginConfigurationUpdatedEvent evt)
    {
        if (evt.UserId == _userId && evt.PluginId == "Last.fm")
        {
            _config = evt.Configuration as LastFmConfiguration;
            await ReinitializeAsync();
        }
    }

    public async Task ReinitializeAsync()
    {
        // Reinitialize HTTP client, auth, etc.
        _httpClient = CreateHttpClient(_config);
    }
}
```

**Alternative: File Watcher for File-Based Configuration**

```csharp
public class LastFMPlugin : IConfigurablePlugin
{
    private readonly Dictionary<Guid, FileSystemWatcher> _watchers = new();

    public void ConfigureServices(IServiceCollection services)
    {
        // ... existing services ...

        // Set up file watchers for configuration changes
        services.AddHostedService<PluginConfigurationWatcherService>();
    }

    private void WatchConfigurationFile(Guid userId)
    {
        var configPath = GetConfigPath(userId);
        var watcher = new FileSystemWatcher(Path.GetDirectoryName(configPath)!)
        {
            Filter = Path.GetFileName(configPath),
            NotifyFilter = NotifyFilters.LastWrite
        };

        watcher.Changed += async (sender, e) =>
        {
            var config = await GetConfiguration(userId);
            if (config != null)
            {
                await OnConfigurationUpdated(userId, config);
            }
        };

        watcher.EnableRaisingEvents = true;
        _watchers[userId] = watcher;
    }
}
```

---

## 8. Plugin Distribution Strategies

### 8.1 Server Plugin Distribution

Server plugins are C# DLL assemblies that extend Coral's backend functionality.

**Installation:**

1. **Download or build the plugin:**
   ```bash
   # Option 1: Download pre-built DLL
   wget https://github.com/coral-plugins/lastfm/releases/download/v1.0.0/Coral.Plugin.LastFM.dll

   # Option 2: Build from source
   git clone https://github.com/coral-plugins/lastfm
   cd lastfm
   dotnet build -c Release
   ```

2. **Copy to server plugins directory:**
   ```bash
   # On the Coral server
   cp Coral.Plugin.LastFM.dll /var/coral/plugins/server/LastFM/
   cp plugin.json /var/coral/plugins/server/LastFM/
   ```

3. **Restart Coral API:**
   ```bash
   systemctl restart coral-api
   # Or restart Docker container
   docker restart coral-api
   ```

**Directory Structure:**

```
/var/coral/plugins/server/
├── LastFM/
│   ├── Coral.Plugin.LastFM.dll
│   ├── plugin.json
│   └── Dependencies.dll (if any)
└── Spotify/
    ├── Coral.Plugin.Spotify.dll
    └── plugin.json
```

### 8.2 Electron Plugin Distribution

Electron plugins are TypeScript/JavaScript modules that extend the desktop app.

**Installation:**

1. **Via npm (recommended):**
   ```bash
   # In Electron app directory
   cd ~/.config/Coral/plugins
   npm install @coral-plugins/discord-rpc
   ```

2. **Manual installation:**
   ```bash
   # Download and extract plugin
   wget https://github.com/coral-plugins/discord-rpc/releases/download/v1.0.0/plugin.tar.gz
   tar -xzf plugin.tar.gz -C ~/.config/Coral/plugins/
   ```

3. **Restart Electron app:**
   - Plugins loaded on app startup
   - No need to rebuild the app

**Directory Structure:**

```
~/.config/Coral/plugins/
├── coral-plugin-discord-rpc/
│   ├── package.json
│   ├── plugin.json
│   └── dist/
│       └── index.js
└── coral-plugin-system-tray/
    ├── package.json
    ├── plugin.json
    └── dist/
        └── index.js
```

**Plugin Package Structure:**

```json
// package.json
{
  "name": "@coral-plugins/discord-rpc",
  "version": "1.0.0",
  "main": "./dist/index.js",
  "files": ["dist", "plugin.json"],
  "scripts": {
    "build": "tsc"
  },
  "peerDependencies": {
    "@coral/plugin-sdk": "^1.0.0"
  },
  "dependencies": {
    "discord-rpc": "^4.0.0"
  }
}
```

### 8.3 Security Considerations

**Server Plugins:**
- Run in the same process as Coral API (full server access)
- Only install plugins from trusted sources
- Review plugin code before installation
- Plugins can access database, file system, and all server APIs

**Electron Plugins:**
- Run with limited capabilities (defined in manifest)
- Sandboxed from main app unless granted IPC access
- Cannot access Node.js APIs unless `electron:native` capability granted
- Storage scoped per plugin

**Update Strategy:**
- Server admin controls all plugin versions
- Breaking changes handled via semantic versioning (Section 3)
- Incompatible plugins refuse to load with clear error messages
- No automatic updates (admin must manually update)

---

## 9. Security & Sandboxing (Electron Plugins)

**Note:** Server plugins run in the Coral API process with full server access. This section applies to Electron plugins only.

### 9.1 Capability-Based Permissions

Electron plugins must declare required capabilities in their manifest. The plugin loader validates capabilities before granting access.

**Available Capabilities:**
- `electron:ipc` - Access to IPC communication with renderer process
- `electron:native` - Access to Node.js and Electron native APIs
- `ui:components` - Ability to register UI components
- `api:access` - Access to Coral API client
- `storage:local` - Access to plugin-scoped local storage
- `network:external` - Access to external network requests

**Validation:**

```typescript
export class PluginCapabilityValidator {
  validate(manifest: PluginManifest, requestedCapability: string): boolean {
    if (!manifest.capabilities.includes(requestedCapability)) {
      throw new PluginSecurityError(
        `Plugin ${manifest.id} requires capability: ${requestedCapability}`
      );
    }
    return true;
  }
}

// Usage in ElectronPluginContext
export class SecureElectronPluginContext implements ElectronPluginContext {
  get electron(): ElectronApis {
    this.requireCapability('electron:native');
    return this._electron;
  }

  private requireCapability(capability: string) {
    if (!this.manifest.capabilities.includes(capability)) {
      throw new PluginSecurityError(
        `Plugin ${this.manifest.id} requires capability: ${capability}`
      );
    }
  }
}
```

### 9.2 Storage Isolation

- Each plugin has its own namespaced storage directory
- Plugins cannot access other plugins' data
- Storage path: `~/.config/Coral/plugin-data/{plugin-id}/`
- Configuration managed separately via schema-driven UI

### 9.3 Network Restrictions

```typescript
// Only allow external network if capability granted
export class PluginNetworkProxy {
  async fetch(url: string) {
    // Check if URL is external
    const isExternal = !url.startsWith(this.context.apiBaseUrl);

    if (isExternal && !this.manifest.capabilities.includes('network:external')) {
      throw new PluginSecurityError(
        `Plugin ${this.manifest.id} attempted external network request without permission`
      );
    }

    return fetch(url);
  }
}
```

---

## 10. Implementation Roadmap

### Phase 1: Version Compatibility (Week 1-2)
- [ ] Create `CoralVersion` class with version constants (server & client)
- [ ] Implement semver compatibility checking (server: NuGet.Versioning, client: semver package)
- [ ] Update server PluginLoader to validate plugin compatibility before loading
- [ ] Add compatibility validation error messages and logging
- [ ] Create migration guide template for breaking changes

### Phase 2: Schema-Driven Configuration (Week 3-4)
- [ ] Add `IConfigurablePlugin` interface to existing server plugins
- [ ] Implement `ConfigurationForm` component with react-hook-form + Zod
- [ ] Create `jsonSchemaToZod` conversion utility
- [ ] Add configuration endpoint to PluginsController
- [ ] Create unified plugin settings UI in coral-app
- [ ] Test with Last.fm plugin (update to use schema-driven config)

### Phase 3: Electron Plugin System (Week 5-6)
- [ ] Create `@coral/plugin-sdk` package for Electron plugins
- [ ] Implement ElectronPluginLoader with compatibility checks
- [ ] Implement capability validation system
- [ ] Add plugin configuration storage for Electron plugins
- [ ] Create example Discord RPC Electron plugin
- [ ] Test plugin loading/unloading lifecycle

### Phase 4: Distribution & Documentation (Week 7-8)
- [ ] Set up npm organization for official plugins
- [ ] Create plugin developer documentation
- [ ] Build plugin CLI tool for scaffolding (`create-coral-plugin`)
- [ ] Document installation procedures for server and Electron plugins
- [ ] Create example plugins repository

### Phase 5: Polish & Security (Week 9-10)
- [ ] Implement plugin error handling and recovery
- [ ] Add plugin compatibility warnings in UI
- [ ] Security audit of plugin sandboxing
- [ ] Add plugin telemetry/logging
- [ ] Create plugin update documentation

---

## 11. Developer Experience

### 11.1 Plugin CLI Tool

```bash
# Scaffold new server plugin (C#)
bunx create-coral-plugin my-plugin --type server

# Generates:
# coral-plugin-myplugin/
#   ├── src/
#   │   ├── MyPlugin.cs
#   │   ├── MyPluginController.cs
#   │   └── MyPluginService.cs
#   ├── plugin.json
#   ├── Coral.Plugin.MyPlugin.csproj
#   └── README.md

# Scaffold new Electron plugin (TypeScript)
bunx create-coral-plugin my-plugin --type electron

# Generates:
# coral-plugin-myplugin/
#   ├── src/
#   │   └── index.ts
#   ├── plugin.json
#   ├── package.json
#   ├── tsconfig.json
#   └── README.md
```

### 11.2 Server Plugin Development

```bash
# Create new server plugin
cd plugins/
dotnet new classlib -n Coral.Plugin.MyPlugin

# Reference plugin base
cd Coral.Plugin.MyPlugin
dotnet add reference ../../src/Coral.PluginBase

# Implement IPlugin and IConfigurablePlugin
# Build plugin
dotnet build -c Release

# Copy to server plugins directory
cp bin/Release/net9.0/Coral.Plugin.MyPlugin.dll /var/coral/plugins/server/MyPlugin/
```

### 11.3 Electron Plugin Development

```bash
# Create new Electron plugin
mkdir coral-plugin-myplugin && cd coral-plugin-myplugin
npm init -y

# Install dependencies
npm install --save-dev @coral/plugin-sdk typescript
npm install your-dependencies

# Implement plugin
# Build
npm run build

# Install for testing
cp -r dist ~/.config/Coral/plugins/coral-plugin-myplugin/
```

### 11.4 Hot Reload During Development

**Server Plugins:**
- No hot reload (requires API restart)
- Use `dotnet watch` for quick rebuild cycles

**Electron Plugins:**
```typescript
// Dev mode only (in Electron main process)
if (process.env.NODE_ENV === 'development') {
  const chokidar = require('chokidar');
  const watcher = chokidar.watch('./dev-plugins');

  watcher.on('change', async (path) => {
    const pluginId = getPluginIdFromPath(path);
    await electronPluginLoader.reload(pluginId);
    console.log(`Reloaded plugin: ${pluginId}`);
  });
}
```

---

## 12. Example Plugins

### 12.1 Last.fm Server Plugin

**Features:**
- Scrobbles tracks after 50% playback threshold
- Schema-driven configuration UI (API key, shared secret, threshold)
- Event-driven scrobbling via TrackPlaybackEventEmitter

**Plugin Implementation:**
See existing implementation in `src/Coral.Plugin.LastFM/` with these additions:

1. **Implement `IConfigurablePlugin`** for schema-driven config
2. **Add `plugin.json` manifest** with configuration schema
3. **Listen to playback events** for scrobbling

**Configuration UI:**
Auto-generated from JSON Schema (Section 4) - users configure via unified settings page on all platforms (web, iOS, Android, Electron).

### 12.2 Discord RPC Electron Plugin

**Features:**
- Shows currently playing track in Discord
- Updates presence on track change
- Customizable status format via schema-driven config

**Full example:** See Section 5.3 for complete implementation with manifest, plugin code, and configuration UI.

---

## Conclusion

This specification provides a **server-centric plugin architecture** that prioritizes self-hosted extensibility while maintaining App Store compliance for mobile apps:

### What This System Provides

- ✅ **Server plugins (primary)** - C# DLL assemblies providing business logic, integrations, and API endpoints
- ✅ **Electron plugins (desktop enhancement)** - TypeScript/React Native plugins for desktop-specific features
- ✅ **Schema-driven configuration** - Auto-generated UIs for all platforms (web, iOS, Android, Electron)
- ✅ **Version compatibility system** - Prevents incompatible plugins from loading after breaking changes
- ✅ **App Store compliance** - No dynamic code loading on iOS/Android
- ✅ **Unified settings UI** - Configure all plugins from a single page across all platforms
- ✅ **Security sandboxing** - Capability-based permissions for Electron plugins
- ✅ **Developer-friendly** - CLI tools, hot reload, and clear documentation
- ✅ **Aligned with existing architecture** - Extends proven server-side plugin patterns

### Key Design Decisions

**1. Server-Centric Philosophy**

Coral is a self-hosted application, so the server is the natural place for extensibility:
- **Server plugins work everywhere**: One C# plugin provides functionality to all clients
- **Mobile apps stay compliant**: No code download = no App Store review issues
- **Maximum flexibility for self-hosters**: Full server access for custom integrations
- **Consistent experience**: Same features available on web, iOS, Android, and Electron

**2. Schema-Driven UI Generation**

Removes the need for custom plugin UI on mobile platforms:
- **JSON Schema → Auto-generated forms**: Plugins describe their config, clients render it
- **Type-safe validation**: Zod schemas ensure correct configuration
- **Consistent UX**: All platforms use the same configuration UI
- **Zero custom plugin UI on mobile**: Fully App Store compliant
- **Electron gets full flexibility**: Desktop plugins can register custom components

**3. Version Compatibility System**

Protects users from incompatible plugins after breaking changes:
- **Semantic versioning**: Clear version semantics for plugins and plugin API
- **Automatic validation**: Plugin loaders check compatibility before loading
- **Graceful degradation**: Incompatible plugins show helpful error messages
- **Independent API versioning**: Plugin API version separate from Coral version
- **Migration guides**: Documentation for upgrading plugins across major versions

**4. Two-Tier Plugin Ecosystem**

- **Tier 1 (Server Plugins)**: Full flexibility, all platforms, self-hosted control
- **Tier 2 (Electron Plugins)**: Desktop enhancements, native integrations, custom UI

This approach ensures maximum functionality while respecting platform limitations and app store policies.

### Trade-offs

**What We Gain:**
- ✅ Guaranteed App Store approval for iOS/Android
- ✅ Server-side business logic reusable across all platforms
- ✅ Simpler client architecture (no dynamic code loading)
- ✅ Consistent configuration UX across all platforms
- ✅ Full flexibility for Electron desktop app

**What We Accept:**
- ⚠️ Mobile apps can't have custom plugin UI (schema-driven only)
- ⚠️ Web app uses same approach as mobile (consistency over flexibility)
- ⚠️ Electron plugins desktop-only (not available on mobile/web)

For a self-hosted music streaming platform, this is the right balance of extensibility, compliance, and maintainability.
