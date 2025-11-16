namespace Coral.Api;

public class ScanJobProgress
{
    public required Guid RequestId { get; init; }
    public required Guid LibraryId { get; init; }
    public required string LibraryName { get; init; }
    public required int ExpectedTracks { get; init; }
    public int TracksIndexed;
    public int EmbeddingsCompleted;
    public required DateTime StartedAt { get; init; }
}

public class ScanProgressDto
{
    public required Guid RequestId { get; init; }
    public required string LibraryName { get; init; }
    public required int TracksIndexed { get; init; }
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
