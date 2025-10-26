using Coral.Database.Models;

namespace Coral.Services;

public interface IIndexerService
{
    Task DeleteTrack(string filePath);
    Task HandleRename(string oldPath, string newPath);
    Task ScanDirectory(string directory, MusicLibrary library);
    Task ScanLibraries();
    Task ScanLibrary(MusicLibrary library, bool incremental = false);
    IAsyncEnumerable<Track> IndexDirectoryGroups(
        IAsyncEnumerable<Indexer.DirectoryGroup> directoryGroups,
        MusicLibrary library,
        CancellationToken cancellationToken = default);
    Task FinalizeIndexing(MusicLibrary library, CancellationToken cancellationToken = default);
}
