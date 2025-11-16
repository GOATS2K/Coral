using Coral.Database.Models;
using Coral.Services.ChannelWrappers;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Coral.Services.Indexer;

namespace Coral.Api;

public interface IScanReporter
{
    void RegisterScan(Guid? requestId, int expectedTracks, MusicLibrary library);
    Task ReportIndexOperation(Guid? requestId, IndexEvent indexEvent);
    Task ReportEmbeddingCompleted(Guid? requestId);
    Task CompleteScan(Guid? requestId);
    Task FailScan(Guid? requestId, string errorMessage);
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
            TracksAdded = 0,
            TracksUpdated = 0,
            TracksDeleted = 0,
            EmbeddingsCompleted = 0,
            StartedAt = DateTime.UtcNow
        };

        _ = _scanJobs.TryAdd(requestId.Value, progress);
    }

    public async Task ReportIndexOperation(Guid? requestId, IndexEvent indexEvent)
    {
        if (requestId == null) return;

        if (!_scanJobs.TryGetValue(requestId.Value, out var progress)) return;
        
        switch (indexEvent.Operation)
        {
            case IndexerOperation.Create:
                progress.TracksAdded += 1;
                break;
            case IndexerOperation.Update:
                progress.TracksUpdated += 1;
                break;
            case IndexerOperation.Delete:
                progress.TracksDeleted += 1;
                break;
        }

        await EmitProgress(requestId.Value, progress);
    }

    public async Task ReportEmbeddingCompleted(Guid? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            progress.EmbeddingsCompleted += 1;
            var completed = progress.EmbeddingsCompleted;
            await EmitProgress(requestId.Value, progress);

            // If all expected tracks have been processed for embeddings, complete the scan
            if (completed >= progress.ExpectedTracks)
            {
                await CompleteScan(requestId);
            }
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
                TracksAdded = progress.TracksAdded,
                TracksUpdated = progress.TracksUpdated,
                TracksDeleted = progress.TracksDeleted,
                EmbeddingsCompleted = progress.EmbeddingsCompleted,
                Duration = duration
            });

            // Remove from active scans
            _scanJobs.TryRemove(requestId.Value, out _);
        }
    }

    public async Task FailScan(Guid? requestId, string errorMessage)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId.Value, out var progress))
        {
            progress.CompletedAt = DateTime.UtcNow;
            progress.IsFailed = true;
            progress.ErrorMessage = errorMessage;
            var duration = progress.CompletedAt.Value - progress.StartedAt;

            // Emit failure event
            await EmitScanFailed(new ScanFailedDto
            {
                RequestId = requestId.Value,
                LibraryName = progress.LibraryName,
                ErrorMessage = errorMessage,
                Duration = duration
            });

            // Remove from active scans
            _scanJobs.TryRemove(requestId.Value, out _);
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

    private async Task EmitProgress(Guid requestId, ScanJobProgress progress)
    {
        await _hubContext.Clients.All.LibraryScanProgress(new ScanProgressDto
        {
            RequestId = requestId,
            LibraryName = progress.LibraryName,
            ExpectedTracks = progress.ExpectedTracks,
            TracksDeleted = progress.TracksDeleted,
            TracksAdded = progress.TracksAdded,
            TracksUpdated = progress.TracksUpdated,
            EmbeddingsCompleted = progress.EmbeddingsCompleted,
        });
    }

    private async Task EmitScanComplete(ScanCompleteDto scanComplete)
    {
        await _hubContext.Clients.All.LibraryScanComplete(scanComplete);
    }

    private async Task EmitScanFailed(ScanFailedDto scanFailed)
    {
        await _hubContext.Clients.All.LibraryScanFailed(scanFailed);
    }
}
