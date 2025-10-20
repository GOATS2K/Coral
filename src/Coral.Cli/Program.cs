using Coral.Configuration;

Console.WriteLine("Coral Configuration Test - Testing live-reload");
Console.WriteLine("Edit the config.json file while this is running to see changes take effect.");
Console.WriteLine($"Config file location: {ApplicationConfiguration.ConfigurationFile}");
Console.WriteLine("Press Ctrl+C to exit.\n");

while (true)
{
    Console.WriteLine($"\n=== Configuration Check at {DateTime.Now:HH:mm:ss} ===");

    Console.WriteLine("Database Settings:");
    Console.WriteLine($"  Host: {ApplicationConfiguration.GetConfiguration()["Database:Host"]}");
    Console.WriteLine($"  Port: {ApplicationConfiguration.GetConfiguration()["Database:Port"]}");
    Console.WriteLine($"  Database: {ApplicationConfiguration.GetConfiguration()["Database:Database"]}");
    Console.WriteLine($"  Connection String: {ApplicationConfiguration.DatabaseConnectionString}");

    Console.WriteLine("Path Settings:");
    Console.WriteLine($"  Data: {ApplicationConfiguration.AppData}");
    Console.WriteLine($"  Thumbnails: {ApplicationConfiguration.Thumbnails}");
    Console.WriteLine($"  HLS: {ApplicationConfiguration.HLSDirectory}");

    Console.WriteLine("Waiting 2 seconds before next refresh...");

    await Task.Delay(2000);
}