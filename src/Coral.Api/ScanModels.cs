namespace Coral.Api;

public class ScanJobProgress
{
    public required string RequestId { get; init; }
    public required Guid LibraryId { get; init; }
    public required string LibraryName { get; init; }
    public required int ExpectedTracks { get; init; }
    public int TracksIndexed;
    public int EmbeddingsCompleted;
    public required DateTime StartedAt { get; init; }
}

public class ScanProgressDto
{
    public required string RequestId { get; init; }
    public required string LibraryName { get; init; }
    public required int TracksIndexed { get; init; }
    public required int EmbeddingsCompleted { get; init; }
}
