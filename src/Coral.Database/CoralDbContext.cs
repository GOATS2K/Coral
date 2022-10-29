using System.Security.Principal;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Coral.Database;

public class CoralDbContext : DbContext
{
    public DbSet<Track> Tracks { get; set; } = null!;
    public DbSet<Artist> Artists { get; set; } = null!;
    public DbSet<Album> Albums { get; set; } = null!;
    public DbSet<Genre> Genres { get; set; } = null!;
    private string DbPath { get; }
    
    public CoralDbContext(DbContextOptions<CoralDbContext> options)
        : base(options)
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Path.Join(Environment.GetFolderPath(folder), "Coral");
        Directory.CreateDirectory(path);
        DbPath = Path.Join(path, "coral.db");
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }
    }
}