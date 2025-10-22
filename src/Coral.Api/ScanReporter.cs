using Coral.Database.Models;
using Coral.Services.ChannelWrappers;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Coral.Api;

public interface IScanReporter
{
    void RegisterScan(string? requestId, int expectedTracks, MusicLibrary library);
    Task ReportTrackIndexed(string? requestId);
    Task ReportEmbeddingCompleted(string? requestId);
    ScanJobProgress? GetProgress(string? requestId);
    List<ScanJobProgress> GetActiveScans();
}

public class ScanReporter : IScanReporter
{
    private readonly ConcurrentDictionary<string, ScanJobProgress> _scanJobs = new();
    private readonly IHubContext<LibraryHub, ILibraryHubClient> _hubContext;

    public ScanReporter(IHubContext<LibraryHub, ILibraryHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public void RegisterScan(string? requestId, int expectedTracks, MusicLibrary library)
    {
        if (requestId == null) return;

        var progress = new ScanJobProgress
        {
            RequestId = requestId,
            LibraryId = library.Id,
            LibraryName = library.LibraryPath,
            ExpectedTracks = expectedTracks,
            TracksIndexed = 0,
            EmbeddingsCompleted = 0,
            StartedAt = DateTime.UtcNow
        };

        _ = _scanJobs.TryAdd(requestId, progress);
    }

    public async Task ReportTrackIndexed(string? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId, out var progress))
        {
            var indexed = Interlocked.Increment(ref progress.TracksIndexed);
            await EmitProgress(requestId, progress.LibraryName, indexed, progress.EmbeddingsCompleted);
        }
    }

    public async Task ReportEmbeddingCompleted(string? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId, out var progress))
        {
            var completed = Interlocked.Increment(ref progress.EmbeddingsCompleted);
            await EmitProgress(requestId, progress.LibraryName, progress.TracksIndexed, completed);

            // Auto-cleanup when scan is fully complete
            if (completed == progress.ExpectedTracks)
            {
                _scanJobs.TryRemove(requestId, out _);
            }
        }
    }

    public ScanJobProgress? GetProgress(string? requestId)
    {
        if (requestId == null) return null;

        return _scanJobs.TryGetValue(requestId, out var progress) ? progress : null;
    }

    public List<ScanJobProgress> GetActiveScans()
    {
        return _scanJobs.Values.ToList();
    }

    private async Task EmitProgress(string requestId, string libraryName, int tracksIndexed, int embeddingsCompleted)
    {
        await _hubContext.Clients.All.LibraryScanProgress(new ScanProgressDto
        {
            RequestId = requestId,
            LibraryName = libraryName,
            TracksIndexed = tracksIndexed,
            EmbeddingsCompleted = embeddingsCompleted
        });
    }
}
