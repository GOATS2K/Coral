namespace Coral.Dto.Models;

public record AlbumRecommendationDto
{
    /// <summary>
    /// The recommended album details
    /// </summary>
    public SimpleAlbumDto Album { get; init; } = null!;

    /// <summary>
    /// Similarity score as a percentage (0-100)
    /// Higher values indicate greater similarity
    /// </summary>
    public int SimilarityPercentage { get; init; }

    /// <summary>
    /// Human-readable similarity label for UI display
    /// </summary>
    public string SimilarityLabel => SimilarityPercentage switch
    {
        >= 90 => "Very Similar",
        >= 75 => "Similar",
        >= 60 => "Somewhat Similar",
        >= 40 => "Related",
        _ => "Loosely Related"
    };
}