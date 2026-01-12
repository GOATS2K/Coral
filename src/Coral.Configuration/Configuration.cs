using System.Text.Json;
using Coral.Configuration.Migrator;
using Coral.Configuration.Models;
using Microsoft.Extensions.Configuration;

namespace Coral.Configuration;

public static class ApplicationConfiguration
{
    private const string ApplicationName = "Coral";
    private const string ConfigFileName = "config.json";

    private static bool RunningInDocker => File.Exists("/.dockerenv");

    private static string ConfigurationDirectory =>
        RunningInDocker
            ? "/config"
            : Path.Combine(GetConfigDirectory(), ApplicationName);

    public static string ConfigurationFile => Path.Combine(ConfigurationDirectory, ConfigFileName);

    private static IConfiguration? _configuration;

    private static string GetConfigDirectory()
    {
        var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrEmpty(folderPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : folderPath;
    }

    public static IConfiguration GetConfiguration()
    {
        if (_configuration == null)
        {
            EnsureDirectoriesCreated();
            EnsureConfigurationCreated();
            UpdateOutdatedConfiguration();

            _configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigurationFile, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables(prefix: "CORAL_")
                .Build();
        }

        return _configuration;
    }

    private static ServerConfiguration Config
    {
        get
        {
            var config = new ServerConfiguration();
            GetConfiguration().Bind(config);
            return config;
        }
    }

    // Existing static properties
    public static string HLSDirectory => Config.Paths.HlsDirectory;
    public static string AppData => Config.Paths.Data;
    public static string Thumbnails => Config.Paths.Thumbnails;
    public static string ExtractedArtwork => Config.Paths.ExtractedArtwork;
    public static string Plugins => Config.Paths.Plugins;
    public static string Models => Config.Paths.Models;

    // Database properties
    public static string SqliteDbPath => Config.Paths.SqliteDbPath;
    public static string DuckDbEmbeddingsPath => Config.Paths.DuckDbEmbeddingsPath;

    private static void EnsureDirectoriesCreated()
    {
        Directory.CreateDirectory(ConfigurationDirectory);
    }

    private static void EnsureConfigurationCreated()
    {
        if (!File.Exists(ConfigurationFile))
        {
            var defaultConfig = CreateDefaultConfiguration();
            WriteConfiguration(defaultConfig);
            Console.WriteLine($"Created default configuration at: {ConfigurationFile}");
        }
    }

    private static void UpdateOutdatedConfiguration()
    {
        try
        {
            var configBytes = File.ReadAllBytes(ConfigurationFile);
            var config = JsonSerializer.Deserialize<ServerConfiguration>(configBytes)!;

            if (config.ConfigVersion < ServerConfiguration.CurrentVersion)
            {
                Console.WriteLine($"Configuration needs migration: v{config.ConfigVersion} → v{ServerConfiguration.CurrentVersion}");
                var migration = new ConfigMigration(config.ConfigVersion, ConfigurationFile);
                migration.Migrate();
                Console.WriteLine("Configuration migration completed successfully.");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error reading configuration: {ex.Message}");
            Console.WriteLine($"Your configuration file at {ConfigurationFile} may be corrupt. Please delete it to regenerate.");
            throw;
        }
    }

    private static ServerConfiguration CreateDefaultConfiguration()
    {
        return new ServerConfiguration
        {
            ConfigVersion = ServerConfiguration.CurrentVersion,
            Paths = new PathSettings
            {
                Data = PathSettings.GetDefaultDataDirectory()
            },
            FileWatcher = new FileWatcherSettings(),
            Jwt = new JwtSettings(),
            Inference = new InferenceSettings()
        };
    }

    public static void WriteConfiguration(ServerConfiguration config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(ConfigurationFile, json);
        Console.WriteLine($"Configuration saved to: {ConfigurationFile}");
    }

    public static void EnsureDirectoriesAreCreated()
    {
        Directory.CreateDirectory(Config.Paths.Data);
        Directory.CreateDirectory(Config.Paths.HlsDirectory);
        Directory.CreateDirectory(Plugins);
        Directory.CreateDirectory(Models);
    }
}