using Coral.Configuration;
using Coral.Database.Configurations;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<AudioFile> AudioFiles { get; set; } = null!;
    public DbSet<MusicLibrary> MusicLibraries { get; set; } = null!;
    public DbSet<AudioMetadata> AudioMetadata { get; set; } = null!;
    public DbSet<RecordLabel> RecordLabels { get; set; } = null!;
    public DbSet<FavoriteArtist> FavoriteArtists { get; set; } = null!;
    public DbSet<FavoriteAlbum> FavoriteAlbums { get; set; } = null!;
    public DbSet<Playlist> Playlists { get; set; } = null!;
    public DbSet<PlaylistTrack> PlaylistTracks { get; set; } = null!;

    // Auth
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Device> Devices { get; set; } = null!;

    public CoralDbContext(DbContextOptions<CoralDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={ApplicationConfiguration.SqliteDbPath}", o =>
            {
                o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure CURRENT_TIMESTAMP defaults for all BaseTable entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseTable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(BaseTable.CreatedAt))
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(BaseTable.UpdatedAt))
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
        }

        // SQLite: Convert string arrays to comma-separated strings
        var stringArrayComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<string[]>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToArray()
        );

        modelBuilder.Entity<Models.Artwork>()
            .Property(a => a.Colors)
            .HasConversion(
                v => string.Join(",", v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
            )
            .Metadata.SetValueComparer(stringArrayComparer);

        // Configure Paths as JSON column containing a list
        modelBuilder.Entity<Models.Artwork>()
            .OwnsMany(a => a.Paths, pb =>
            {
                pb.ToJson();
            });

        // Performance: Index on AlbumId for artwork queries (no longer need Size since it's in JSON)
        modelBuilder.Entity<Models.Artwork>()
            .HasIndex(a => a.AlbumId);

        // Performance: Composite indexes for join tables to improve join performance
        modelBuilder.Entity<Models.Track>()
            .HasIndex(t => new { t.AlbumId, t.DiscNumber, t.TrackNumber });

        modelBuilder.Entity<Models.ArtistWithRole>()
            .HasIndex(a => new { a.ArtistId, a.Role });

        // Auth: Unique indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }
}