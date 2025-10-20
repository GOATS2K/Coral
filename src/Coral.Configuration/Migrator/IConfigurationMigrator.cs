using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrator;

internal interface IConfigurationMigrator
{
    int TargetVersion { get; }
    int DestinationVersion { get; }

    void Migrate(JsonNode config);
}
