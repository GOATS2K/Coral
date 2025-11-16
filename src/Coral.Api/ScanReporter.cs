using Coral.Database.Models;
using Coral.Services.ChannelWrappers;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Coral.Api;

public interface IScanReporter
{
    void RegisterScan(Guid? requestId, int expectedTracks, MusicLibrary library);
    Task ReportTrackIndexed(Guid? requestId);
    Task ReportEmbeddingCompleted(Guid? requestId);
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

    public async Task ReportEmbeddingCompleted(Guid? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            var completed = Interlocked.Increment(ref progress.EmbeddingsCompleted);
            await EmitProgress(requestId.Value, progress.LibraryName, progress.TracksIndexed, completed);

            // Auto-cleanup when scan is fully complete
            if (completed == progress.ExpectedTracks)
            {
                _scanJobs.TryRemove(requestId.Value, out _);
            }
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
}
