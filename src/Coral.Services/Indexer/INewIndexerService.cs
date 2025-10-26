using Coral.Database.Models;

namespace Coral.Services.Indexer;

/// <summary>
/// High-performance indexer interface with streaming support for benchmarks.
/// </summary>
public interface INewIndexerService
{
    /// <summary>
    /// Indexes directory groups and yields tracks as they are indexed (streaming).
    /// Used for progress reporting in benchmarks.
    /// </summary>
    IAsyncEnumerable<Track> IndexDirectoryGroups(
        IAsyncEnumerable<DirectoryGroup> directoryGroups,
        MusicLibrary library,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes indexing by saving all pending bulk operations and processing artworks/keywords.
    /// Must be called after IndexDirectoryGroups completes.
    /// </summary>
    Task FinalizeIndexing(MusicLibrary library, CancellationToken cancellationToken = default);
}
