using Coral.Configuration;
using Coral.Database.Configurations;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Coral.Database;

public class CoralDbContext : DbContext
{
    public DbSet<Artwork> Artworks { get; set; }
    public DbSet<Track> Tracks { get; set; } = null!;
    public DbSet<Artist> Artists { get; set; } = null!;
    public DbSet<ArtistWithRole> ArtistsWithRoles { get; set; } = null!;
    public DbSet<Album> Albums { get; set; } = null!;
    public DbSet<Genre> Genres { get; set; } = null!;
    public DbSet<Keyword> Keywords { get; set; } = null!;
    private string DbPath { get; }


    public CoralDbContext(DbContextOptions<CoralDbContext> options)
        : base(options)
    {
        var path = ApplicationConfiguration.AppData;
        Directory.CreateDirectory(path);
        DbPath = Path.Join(path, "Coral.db");
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={DbPath}", opt => opt.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            // options.EnableSensitiveDataLogging();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KeywordConfiguration).Assembly);
        
        // do not create the inherited base table
        modelBuilder.Ignore<BaseTable>();

        var tableTypes = GetDatabaseTableTypes();
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!tableTypes.Contains(entityType.ClrType))
                continue;

            // https://learn.microsoft.com/en-us/ef/core/modeling/entity-types?tabs=fluent-api#table-name
            modelBuilder.Entity(entityType.ClrType).ToTable(entityType.ClrType.Name);
        }
    }

    private static Type[] GetDatabaseTableTypes()
    {
        return typeof(CoralDbContext).GetProperties()
            .Where(p => typeof(IQueryable).IsAssignableFrom(p.PropertyType))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToArray();

    }
}