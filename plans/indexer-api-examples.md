# Indexer Refactor - API Examples

## LibraryController Additions

```csharp
[Route("api/[controller]")]
[ApiController]
public class LibraryController : ControllerBase
{
    private readonly IScanReporter _scanReporter;
    // ... other dependencies

    // NEW: Get active scan jobs (for frontend reconnection)
    [HttpGet]
    [Route("scans/active")]
    public ActionResult<List<ActiveScanDto>> GetActiveScans()
    {
        var activeScans = _scanReporter.GetActiveScans();

        var dtos = activeScans.Select(s => new ActiveScanDto
        {
            RequestId = s.RequestId,
            LibraryId = s.LibraryId,
            LibraryPath = s.LibraryPath,
            ExpectedTracks = s.ExpectedTracks,
            TracksIndexed = s.TracksIndexed,
            EmbeddingsCompleted = s.EmbeddingsCompleted,
            IndexingProgress = s.ExpectedTracks > 0
                ? (int)((double)s.TracksIndexed / s.ExpectedTracks * 100)
                : 0,
            EmbeddingProgress = s.ExpectedTracks > 0
                ? (int)((double)s.EmbeddingsCompleted / s.ExpectedTracks * 100)
                : 0,
            StartedAt = s.StartedAt
        }).ToList();

        return Ok(dtos);
    }

    // NEW: Get specific scan progress
    [HttpGet]
    [Route("scans/{requestId}")]
    public ActionResult<ActiveScanDto> GetScanProgress(string requestId)
    {
        var scan = _scanReporter.GetProgress(requestId);
        if (scan == null)
            return NotFound(new { Message = "Scan not found or already completed" });

        var dto = new ActiveScanDto
        {
            RequestId = scan.RequestId,
            LibraryId = scan.LibraryId,
            LibraryPath = scan.LibraryPath,
            ExpectedTracks = scan.ExpectedTracks,
            TracksIndexed = scan.TracksIndexed,
            EmbeddingsCompleted = scan.EmbeddingsCompleted,
            IndexingProgress = scan.ExpectedTracks > 0
                ? (int)((double)scan.TracksIndexed / scan.ExpectedTracks * 100)
                : 0,
            EmbeddingProgress = scan.ExpectedTracks > 0
                ? (int)((double)scan.EmbeddingsCompleted / scan.ExpectedTracks * 100)
                : 0,
            StartedAt = scan.StartedAt
        };

        return Ok(dto);
    }
}

public record ActiveScanDto
{
    public string RequestId { get; set; } = null!;
    public Guid LibraryId { get; set; }
    public string LibraryPath { get; set; } = null!;
    public int ExpectedTracks { get; set; }
    public int TracksIndexed { get; set; }
    public int EmbeddingsCompleted { get; set; }
    public int IndexingProgress { get; set; }
    public int EmbeddingProgress { get; set; }
    public DateTime StartedAt { get; set; }
}
```

## Frontend Usage Examples

### Example 1: User Triggers Scan

```typescript
// User clicks "Scan Library" button
async function startLibraryScan(libraryId: string) {
  const response = await fetch(`/api/library/scan`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ libraryId })
  });

  const { requestId } = await response.json();

  // Subscribe to SignalR updates for this scan
  hubConnection.on('ScanProgress', (progress) => {
    if (progress.requestId === requestId) {
      updateScanUI(progress);
    }
  });

  return requestId;
}

function updateScanUI(progress) {
  // progress = {
  //   requestId: "abc-123",
  //   tracksIndexed: 3500,
  //   embeddingsCompleted: 1200,
  //   totalTracks: 7000,
  //   indexingProgress: 50,
  //   embeddingProgress: 17
  // }

  document.getElementById('indexing-bar').style.width = `${progress.indexingProgress}%`;
  document.getElementById('embedding-bar').style.width = `${progress.embeddingProgress}%`;
  document.getElementById('status').textContent =
    `Indexed ${progress.tracksIndexed}/${progress.totalTracks} tracks, ` +
    `Embeddings ${progress.embeddingsCompleted}/${progress.totalTracks}`;
}
```

### Example 2: Frontend Reconnects After Disconnect

```typescript
// SignalR connection re-established after network issue
hubConnection.onreconnected(async () => {
  console.log('SignalR reconnected, checking for active scans...');

  // Get all active scans
  const response = await fetch('/api/library/scans/active');
  const activeScans = await response.json();

  if (activeScans.length === 0) {
    console.log('No active scans');
    return;
  }

  // Restore UI state for each active scan
  activeScans.forEach(scan => {
    console.log(`Found active scan: ${scan.requestId} for ${scan.libraryPath}`);

    // Show scan progress UI
    showScanProgressUI({
      requestId: scan.requestId,
      libraryPath: scan.libraryPath,
      indexingProgress: scan.indexingProgress,
      embeddingProgress: scan.embeddingProgress,
      tracksIndexed: scan.tracksIndexed,
      embeddingsCompleted: scan.embeddingsCompleted,
      totalTracks: scan.expectedTracks
    });
  });

  // Re-subscribe to updates
  hubConnection.on('ScanProgress', (progress) => {
    const activeScan = activeScans.find(s => s.requestId === progress.requestId);
    if (activeScan) {
      updateScanUI(progress);
    }
  });
});
```

### Example 3: Poll Specific Scan Progress (Fallback)

```typescript
// If SignalR is unavailable, poll for progress
async function pollScanProgress(requestId: string) {
  const interval = setInterval(async () => {
    try {
      const response = await fetch(`/api/library/scans/${requestId}`);

      if (response.status === 404) {
        // Scan completed or doesn't exist
        console.log('Scan completed');
        clearInterval(interval);
        return;
      }

      const progress = await response.json();
      updateScanUI(progress);

      // Stop polling if both indexing and embeddings are complete
      if (progress.indexingProgress === 100 && progress.embeddingProgress === 100) {
        clearInterval(interval);
      }
    } catch (error) {
      console.error('Failed to fetch scan progress', error);
    }
  }, 2000); // Poll every 2 seconds
}
```

### Example 4: Multiple Concurrent Scans

```typescript
// User has 3 music libraries and scans all of them at once
async function scanAllLibraries() {
  const libraries = await fetch('/api/library/all').then(r => r.json());

  const scanRequests = libraries.map(async (library) => {
    const response = await fetch('/api/library/scan', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ libraryId: library.id })
    });

    return response.json();
  });

  const scans = await Promise.all(scanRequests);
  // scans = [
  //   { requestId: "scan-1" },
  //   { requestId: "scan-2" },
  //   { requestId: "scan-3" }
  // ]

  // Subscribe to all scan progress updates
  hubConnection.on('ScanProgress', (progress) => {
    const scanCard = document.getElementById(`scan-${progress.requestId}`);
    if (scanCard) {
      // Update individual scan card
      scanCard.querySelector('.indexing-bar').style.width = `${progress.indexingProgress}%`;
      scanCard.querySelector('.embedding-bar').style.width = `${progress.embeddingProgress}%`;
    }
  });
}
```

## SignalR Hub Example

```csharp
public class IndexerHub : Hub
{
    private readonly IScanReporter _scanReporter;

    public IndexerHub(IScanReporter scanReporter)
    {
        _scanReporter = scanReporter;
    }

    // Client can request current progress on connect
    public async Task<List<ActiveScanDto>> GetActiveScans()
    {
        var activeScans = _scanReporter.GetActiveScans();

        return activeScans.Select(s => new ActiveScanDto
        {
            RequestId = s.RequestId,
            LibraryId = s.LibraryId,
            LibraryPath = s.LibraryPath,
            ExpectedTracks = s.ExpectedTracks,
            TracksIndexed = s.TracksIndexed,
            EmbeddingsCompleted = s.EmbeddingsCompleted,
            IndexingProgress = s.ExpectedTracks > 0
                ? (int)((double)s.TracksIndexed / s.ExpectedTracks * 100)
                : 0,
            EmbeddingProgress = s.ExpectedTracks > 0
                ? (int)((double)s.EmbeddingsCompleted / s.ExpectedTracks * 100)
                : 0,
            StartedAt = s.StartedAt
        }).ToList();
    }
}
```

## Summary

**Key Endpoints:**
- `GET /api/library/scans/active` - Get all active scans (for reconnection)
- `GET /api/library/scans/{requestId}` - Get specific scan progress
- `POST /api/library/scan` - Start a new scan (returns requestId)

**SignalR Events:**
- `ScanProgress` - Real-time progress updates (broadcasted on every track/embedding completion)

**Frontend Flow:**
1. Start scan → receive requestId
2. Subscribe to SignalR `ScanProgress` events
3. If disconnected → reconnect → fetch active scans → restore UI state
4. Poll as fallback if SignalR unavailable
