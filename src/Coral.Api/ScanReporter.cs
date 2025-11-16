using Coral.Database.Models;
using Coral.Services.ChannelWrappers;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Coral.Api;

public interface IScanReporter
{
    void RegisterScan(Guid? requestId, int expectedTracks, MusicLibrary library);
    Task ReportTrackIndexed(Guid? requestId);
    Task ReportTracksDeleted(Guid? requestId, int count);
    Task ReportTracksUnchanged(Guid? requestId, int count);
    Task ReportEmbeddingCompleted(Guid? requestId);
    Task CompleteScan(Guid? requestId);
    void CleanupOldScans(TimeSpan olderThan);
    ScanJobProgress? GetProgress(Guid? requestId);
    List<ScanJobProgress> GetActiveScans();
}

public class ScanReporter : IScanReporter
{
    private readonly ConcurrentDictionary<Guid, ScanJobProgress> _scanJobs = new();
    private readonly IHubContext<LibraryHub, ILibraryHubClient> _hubContext;

    public ScanReporter(IHubContext<LibraryHub, ILibraryHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public void RegisterScan(Guid? requestId, int expectedTracks, MusicLibrary library)
    {
        if (requestId == null) return;

        var progress = new ScanJobProgress
        {
            RequestId = requestId.Value,
            LibraryId = library.Id,
            LibraryName = library.LibraryPath,
            ExpectedTracks = expectedTracks,
            TracksIndexed = 0,
            EmbeddingsCompleted = 0,
            StartedAt = DateTime.UtcNow
        };

        _ = _scanJobs.TryAdd(requestId.Value, progress);
    }

    public async Task ReportTrackIndexed(Guid? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            var indexed = Interlocked.Increment(ref progress.TracksIndexed);
            await EmitProgress(requestId.Value, progress.LibraryName, indexed, progress.EmbeddingsCompleted);
        }
    }

    public async Task ReportTracksDeleted(Guid? requestId, int count)
    {
        if (requestId == null || count == 0) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            Interlocked.Add(ref progress.TracksDeleted, count);
            // Don't emit progress for deletions, just track internally
        }
    }

    public async Task ReportTracksUnchanged(Guid? requestId, int count)
    {
        if (requestId == null || count == 0) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            Interlocked.Add(ref progress.TracksUnchanged, count);
            // Don't emit progress for unchanged, just track internally
        }
    }

    public async Task ReportEmbeddingCompleted(Guid? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            var completed = Interlocked.Increment(ref progress.EmbeddingsCompleted);
            await EmitProgress(requestId.Value, progress.LibraryName, progress.TracksIndexed, completed);

            // Note: Don't auto-remove here as it won't work correctly for incremental scans
            // The IndexerService should explicitly call CompleteScan when done
        }
    }

    public async Task CompleteScan(Guid? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            progress.CompletedAt = DateTime.UtcNow;
            var duration = progress.CompletedAt.Value - progress.StartedAt;

            // Emit completion event with full statistics
            await EmitScanComplete(new ScanCompleteDto
            {
                RequestId = requestId.Value,
                LibraryName = progress.LibraryName,
                TracksAdded = progress.TracksIndexed,
                TracksDeleted = progress.TracksDeleted,
                TracksUnchanged = progress.TracksUnchanged,
                EmbeddingsCompleted = progress.EmbeddingsCompleted,
                Duration = duration
            });

            // Remove from active scans
            _scanJobs.TryRemove(requestId.Value, out _);
        }
    }

    public void CleanupOldScans(TimeSpan olderThan)
    {
        var cutoffTime = DateTime.UtcNow - olderThan;
        var staleScans = _scanJobs
            .Where(kvp => kvp.Value.StartedAt < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var requestId in staleScans)
        {
            _scanJobs.TryRemove(requestId, out _);
        }
    }

    public ScanJobProgress? GetProgress(Guid? requestId)
    {
        if (requestId == null) return null;

        return _scanJobs.TryGetValue(requestId.Value, out var progress) ? progress : null;
    }

    public List<ScanJobProgress> GetActiveScans()
    {
        return _scanJobs.Values.ToList();
    }

    private async Task EmitProgress(Guid requestId, string libraryName, int tracksIndexed, int embeddingsCompleted)
    {
        await _hubContext.Clients.All.LibraryScanProgress(new ScanProgressDto
        {
            RequestId = requestId,
            LibraryName = libraryName,
            TracksIndexed = tracksIndexed,
            EmbeddingsCompleted = embeddingsCompleted
        });
    }

    private async Task EmitScanComplete(ScanCompleteDto scanComplete)
    {
        await _hubContext.Clients.All.LibraryScanComplete(scanComplete);
    }
}
