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
    public DbSet<TrackEmbedding> TrackEmbeddings { get; set; } = null!;
    public DbSet<FavoriteTrack> FavoriteTracks { get; set; } = null!;
    public DbSet<FavoriteArtist> FavoriteArtists { get; set; } = null!;
    public DbSet<FavoriteAlbum> FavoriteAlbums { get; set; } = null!;
    public DbSet<Playlist> Playlists { get; set; } = null!;
    public DbSet<PlaylistTrack> PlaylistTracks { get; set; } = null!;


    public CoralDbContext(DbContextOptions<CoralDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseNpgsql(ApplicationConfiguration.DatabaseConnectionString, opt => opt.UseVector());
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        
        modelBuilder.Entity<TrackEmbedding>()
            .HasIndex(i => i.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);
    }
}