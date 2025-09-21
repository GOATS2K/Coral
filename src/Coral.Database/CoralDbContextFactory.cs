using Coral.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coral.Database;

public class CoralDbContextFactory : IDesignTimeDbContextFactory<CoralDbContext>
{
    public CoralDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoralDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Username=postgres;Password=postgres;Database=coral2", opt => opt.UseVector());
        return new CoralDbContext(optionsBuilder.Options);
    }
}