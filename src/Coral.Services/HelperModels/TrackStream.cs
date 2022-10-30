namespace Coral.Services.HelperModels;

public record TrackStream
{
    public Stream Stream { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long Length { get; init; } = default!;
    public string FileName { get; init; } = default!;
}