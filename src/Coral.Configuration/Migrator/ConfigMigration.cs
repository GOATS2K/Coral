using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrator;

public class ConfigMigration
{
    private readonly List<IConfigurationMigrator> _migrators;
    private readonly string _configFilePath;

    public ConfigMigration(int fromVersion, string configFilePath)
    {
        _configFilePath = configFilePath;
        _migrators = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IConfigurationMigrator).IsAssignableFrom(t) && !t.IsInterface)
            .Select(t => (IConfigurationMigrator)Activator.CreateInstance(t)!)
            .Where(m => m.TargetVersion >= fromVersion)
            .OrderBy(m => m.TargetVersion)
            .ToList();
    }

    public void Migrate()
    {
        foreach (var migrator in _migrators)
        {
            var configBytes = File.ReadAllBytes(_configFilePath);
            var config = JsonSerializer.Deserialize<JsonNode>(configBytes)!;

            // Create backup before migration
            var backupPath = _configFilePath.Replace(".json", $".v{migrator.TargetVersion}.json.bak");
            File.Copy(_configFilePath, backupPath, overwrite: true);

            Console.WriteLine($"Migrating configuration: v{migrator.TargetVersion} → v{migrator.DestinationVersion}");

            try
            {
                migrator.Migrate(config);

                // Write updated config
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_configFilePath, json);

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
