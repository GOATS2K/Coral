using Coral.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coral.Database;

public class CoralDbContextFactory : IDesignTimeDbContextFactory<CoralDbContext>
{
    private string DbPath { get; set; }
    
    public CoralDbContextFactory()
    {
        var path = ApplicationConfiguration.AppData;
        Directory.CreateDirectory(path);
        DbPath = Path.Join(path, "Coral.db");
    }
    
    public CoralDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoralDbContext>();
        optionsBuilder.UseSqlite($"Data Source={DbPath}",
            opt => opt.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        return new CoralDbContext(optionsBuilder.Options);
    }
}