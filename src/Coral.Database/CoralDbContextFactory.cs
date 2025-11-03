using Coral.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coral.Database;

public class CoralDbContextFactory : IDesignTimeDbContextFactory<CoralDbContext>
{
    public CoralDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoralDbContext>();
        optionsBuilder.UseSqlite($"Data Source={ApplicationConfiguration.SqliteDbPath}");
        return new CoralDbContext(optionsBuilder.Options);
    }
}