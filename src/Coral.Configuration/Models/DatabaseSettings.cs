using System.Text.Json.Serialization;

namespace Coral.Configuration.Models;

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
