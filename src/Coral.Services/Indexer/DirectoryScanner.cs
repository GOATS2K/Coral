using Coral.Database;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coral.Services.Indexer;

public interface IDirectoryScanner
{
    Task<int> CountFiles(MusicLibrary library, bool incremental = false);
    IAsyncEnumerable<DirectoryGroup> ScanLibrary(MusicLibrary library, bool incremental = false);
}

public record DirectoryGroup(
    string DirectoryPath,
    List<FileInfo> Files
);

public class DirectoryScanner : IDirectoryScanner
{
    private readonly CoralDbContext _context;
    private readonly ILogger<DirectoryScanner> _logger;

    private static readonly string[] AudioFileFormats =
        [".flac", ".mp3", ".mp2", ".wav", ".m4a", ".ogg", ".alac", ".aif", ".opus"];

    public DirectoryScanner(
        CoralDbContext context,
        ILogger<DirectoryScanner> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CountFiles(MusicLibrary library, bool incremental = false)
    {
        var contentDirectory = new DirectoryInfo(library.LibraryPath);

        if (!incremental)
        {
            // Match ScanLibrary logic - count only files that need processing
            var existingFiles = await _context.AudioFiles
                .Where(f => f.Library.Id == library.Id)
                .ToListAsync();

            return contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Count(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) &&
                           !existingFiles.Any(ef => ef.FilePath == f.FullName &&
                                                    f.LastWriteTimeUtc == ef.UpdatedAt));
        }

        // Incremental mode - count only new/modified files since last scan
        return contentDirectory
            .EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Count(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) &&
                        (f.LastWriteTimeUtc > library.LastScan || f.CreationTimeUtc > library.LastScan));
    }

    public async IAsyncEnumerable<DirectoryGroup> ScanLibrary(
        MusicLibrary library,
        bool incremental = false)
    {
        var contentDirectory = new DirectoryInfo(library.LibraryPath);

        if (!incremental)
        {
            _logger.LogInformation("Starting full scan of directory: {Directory}", library.LibraryPath);
            var existingFiles = await _context.AudioFiles
                .Where(f => f.Library.Id == library.Id)
                .ToListAsync();

            // Group files by directory, yield each group
            var groups = contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) &&
                           !existingFiles.Any(ef => ef.FilePath == f.FullName &&
                                                    f.LastWriteTimeUtc == ef.UpdatedAt))
                .GroupBy(f => f.Directory?.FullName ?? "");

            foreach (var group in groups)
            {
                yield return new DirectoryGroup(group.Key, group.ToList());
            }
        }
        else
        {
            _logger.LogInformation("Starting incremental scan of directory: {Directory}", library.LibraryPath);

            var groups = contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) &&
                           (f.LastWriteTimeUtc > library.LastScan || f.CreationTimeUtc > library.LastScan))
                .GroupBy(f => f.Directory?.FullName ?? "");

            foreach (var group in groups)
            {
                yield return new DirectoryGroup(group.Key, group.ToList());
            }
        }
    }
}
