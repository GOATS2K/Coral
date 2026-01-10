using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Coral.Configuration.Migrator;

/// <summary>
/// Migrates configuration from v1 to v2.
/// Changes:
/// - Adds JWT settings section with 256-bit secret
/// - Adds SessionExpirationDays and TokenExpirationDays
/// </summary>
internal class ConfigMigrator1 : IConfigurationMigrator
{
    public int TargetVersion => 1;
    public int DestinationVersion => 2;

    public void Migrate(JsonNode config)
    {
        // Add JWT section if it doesn't exist
        if (config["Jwt"] == null)
        {
            config["Jwt"] = new JsonObject
            {
                ["Secret"] = GenerateSecret(),
                ["SessionExpirationDays"] = 30
            };
        }
        else
        {
            // Ensure all JWT properties exist
            var jwt = config["Jwt"]!.AsObject();

            if (jwt["Secret"] == null)
            {
                jwt["Secret"] = GenerateSecret();
            }

            if (jwt["SessionExpirationDays"] == null)
            {
                jwt["SessionExpirationDays"] = 30;
            }
        }

        // Update version
        config["ConfigVersion"] = DestinationVersion;
    }

    private static string GenerateSecret()
    {
        // Generate a 256-bit (32 bytes) cryptographically secure secret
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
