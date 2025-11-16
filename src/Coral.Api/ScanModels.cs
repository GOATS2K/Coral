namespace Coral.Api;

public class ScanJobProgress
{
    public required Guid RequestId { get; init; }
    public required Guid LibraryId { get; init; }
    public required string LibraryName { get; init; }
    public required int ExpectedTracks { get; init; }
    public int TracksAdded { get; set; }
    public int TracksUpdated { get; set; }
    public int TracksDeleted { get; set; }
    public int EmbeddingsCompleted { get; set; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public bool IsFailed { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ScanProgressDto
{
    public required Guid RequestId { get; init; }
    public required string LibraryName { get; init; }
    public required int ExpectedTracks { get; init; }
    public required int TracksAdded { get; init; }
    public required int TracksUpdated { get; init; }
    public required int TracksDeleted { get; init; }
    public required int EmbeddingsCompleted { get; init; }
}

public class ScanInitiatedDto
{
    public required List<ScanRequestInfo> Scans { get; init; }
}

public class ScanRequestInfo
{
    public required Guid RequestId { get; init; }
    public required Guid LibraryId { get; init; }
    public required string LibraryName { get; init; }
}

public class ScanCompleteDto
{
    public required Guid RequestId { get; init; }
    public required string LibraryName { get; init; }
    public required int TracksAdded { get; init; }
    public required int TracksDeleted { get; init; }
    public required int TracksUpdated { get; init; }
    public required int EmbeddingsCompleted { get; init; }
    public required TimeSpan Duration { get; init; }
}

public class ScanFailedDto
{
    public required Guid RequestId { get; init; }
    public required string LibraryName { get; init; }
    public required string ErrorMessage { get; init; }
    public required TimeSpan Duration { get; init; }
}
