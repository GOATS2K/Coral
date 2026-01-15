using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrator;

/// <summary>
/// Migrates configuration from v3 to v4.
/// Changes:
/// - Adds ScheduledTasks settings section with ScanOnStartup and LibraryScanIntervalMinutes
/// </summary>
internal class ConfigMigrator3 : IConfigurationMigrator
{
    public int TargetVersion => 3;
    public int DestinationVersion => 4;

    public void Migrate(JsonNode config)
    {
        // Add ScheduledTasks section if it doesn't exist
        if (config["ScheduledTasks"] == null)
        {
            config["ScheduledTasks"] = new JsonObject
            {
                ["ScanOnStartup"] = true,
                ["LibraryScanIntervalMinutes"] = 60
            };
        }
        else
        {
            var scheduledTasks = config["ScheduledTasks"]!.AsObject();
            if (scheduledTasks["ScanOnStartup"] == null)
            {
                scheduledTasks["ScanOnStartup"] = true;
            }
            if (scheduledTasks["LibraryScanIntervalMinutes"] == null)
            {
                scheduledTasks["LibraryScanIntervalMinutes"] = 60;
            }
        }

        // Update version
        config["ConfigVersion"] = DestinationVersion;
    }
}
