using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrator;

/// <summary>
/// Migrates configuration from v2 to v3.
/// Changes:
/// - Adds Inference settings section with MaxConcurrentInstances
/// </summary>
internal class ConfigMigrator2 : IConfigurationMigrator
{
    public int TargetVersion => 2;
    public int DestinationVersion => 3;

    public void Migrate(JsonNode config)
    {
        // Add Inference section if it doesn't exist
        if (config["Inference"] == null)
        {
            config["Inference"] = new JsonObject
            {
                ["MaxConcurrentInstances"] = 4
            };
        }
        else
        {
            var inference = config["Inference"]!.AsObject();
            if (inference["MaxConcurrentInstances"] == null)
            {
                inference["MaxConcurrentInstances"] = 4;
            }
        }

        // Update version
        config["ConfigVersion"] = DestinationVersion;
    }
}
