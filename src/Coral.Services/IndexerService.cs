using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coral.Database;
using Coral.Database.Models;

namespace Coral.Services;

public interface IIndexerService
{
    public IAsyncEnumerable<Track> GetTracks();
    public void IndexDirectory(string directory);
    public void IndexFile(FileInfo filePath);
}

public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private static readonly string[] AudioFileFormats = {".flac", ".mp3", ".wav", ".m4a", ".ogg", ".alac"};
    private static readonly string[] ImageFileFormats = {".jpg", ".png"};
    private static readonly string[] ImageFileNames = {"cover", "artwork", "folder", "front"};

    public IndexerService(CoralDbContext context)
    {
        _context = context;
    }

    public IAsyncEnumerable<Track> GetTracks()
    {
        return _context.Tracks.AsAsyncEnumerable();
    }

    public void IndexDirectory(string directory)
    {
        var contentDirectory = new DirectoryInfo(directory);
        if (!contentDirectory.Exists)
        {
            throw new ApplicationException("Content directory does not exist.");
        }

        foreach (var fileToIndex in contentDirectory
                     .EnumerateFiles("*.*", SearchOption.AllDirectories)
                     .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName))))
        {
            IndexFile(fileToIndex);
        }
    }

    public void IndexFile(FileInfo filePath)
    {
        var indexedTrack = _context.Tracks.FirstOrDefault(t => t.FilePath == filePath.FullName);
        if (indexedTrack != null)
        {
            return;
        }

        var atlTrack = new ATL.Track(filePath.FullName);
        var indexedArtist = GetArtist(atlTrack);
        var indexedAlbum = GetAlbum(indexedArtist, atlTrack);
        var indexedGenre = GetGenre(atlTrack);
        
        indexedTrack = new Track()
        {
            Album = indexedAlbum,
            Artist = indexedArtist,
            Title = atlTrack.Title,
            Comment = atlTrack.Comment,
            Genre = !string.IsNullOrEmpty(atlTrack.Genre) ? indexedGenre : null,
            DateIndexed = DateTime.UtcNow,
            DiscNumber = atlTrack.DiscNumber,
            TrackNumber = atlTrack.TrackNumber,
            DurationInSeconds = atlTrack.Duration,
            FilePath = filePath.FullName
        };
        _context.Tracks.Add(indexedTrack);
        _context.SaveChanges();
    }

    private Genre GetGenre(ATL.Track atlTrack)
    {
        var indexedGenre = _context.Genres.FirstOrDefault(g => g.Name == atlTrack.Genre);
        if (indexedGenre == null)
        {
            indexedGenre = new Genre()
            {
                Name = atlTrack.Genre,
                DateIndexed = DateTime.UtcNow
            };
            _context.Genres.Add(indexedGenre);
        }

        return indexedGenre;
    }
    
    private Artist GetArtist(ATL.Track atlTrack)
    {
        var indexedArtist = _context.Artists.FirstOrDefault(a => a.Name == atlTrack.Artist);
        if (indexedArtist == null)
        {
            indexedArtist = new Artist()
            {
                Name = atlTrack.Artist,
                DateIndexed = DateTime.UtcNow
            };
            _context.Artists.Add(indexedArtist);
        }
        return indexedArtist;
    }

    private string? GetAlbumArtwork(ATL.Track atlTrack)
    {
        // get artwork from file parent folder
        var albumDirectory = new DirectoryInfo(atlTrack.Path)
            .Parent;

        var artwork = albumDirectory?.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => ImageFileFormats.Contains(Path.GetExtension(f.FullName)) 
                                 && ImageFileNames.Contains(f.Name.ToLowerInvariant()));
        
        return artwork?.FullName;
    }
    
    private Album GetAlbum(Artist artist, ATL.Track atlTrack)
    {
        var indexedAlbum = _context.Albums.FirstOrDefault(a => a.Name == atlTrack.Album
                                                               && a.Artists.Any(a => a.Name == artist.Name));
        if (indexedAlbum == null)
        {
            indexedAlbum = new Album()
            {
                Artists = new List<Artist>()
                {
                    artist
                },
                Name = atlTrack.Album,
                ReleaseYear = atlTrack.Year,
                DiscTotal = atlTrack.DiscTotal,
                TrackTotal = atlTrack.TrackTotal,
                DateIndexed = DateTime.UtcNow,
                CoverFilePath = GetAlbumArtwork(atlTrack)
            };
            _context.Albums.Add(indexedAlbum);
        }

        if (indexedAlbum.Artists.All(a => a.Name != artist.Name))
        {
            indexedAlbum.Artists.Add(artist);
            _context.SaveChanges();
        }
        return indexedAlbum;
    }
}