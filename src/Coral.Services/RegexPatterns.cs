using System.Text.RegularExpressions;

namespace Coral.Services
{
    /// <summary>
    /// Contains source-generated regex patterns for search and parsing operations.
    /// </summary>
    internal static partial class RegexPatterns
    {
        /// <summary>
        /// Source-generated regex for extracting keywords from input strings.
        /// Matches Unicode letters (\p{L}) and decimal numbers (\p{Nd}).
        /// </summary>
        [GeneratedRegex(@"[\p{L}\p{Nd}]+", RegexOptions.IgnoreCase)]
        internal static partial Regex KeywordExtraction();

        /// <summary>
        /// Source-generated regex for parsing remixer artists from track titles.
        /// Supports both (artist remix) and [artist remix] formats.
        /// </summary>
        [GeneratedRegex(@"\(([^()]*)(?: Edit| Remix| VIP| Bootleg)\)|\[([^[\[\]]*)(?: Edit| Remix| VIP| Bootleg)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        internal static partial Regex RemixerParsing();

        /// <summary>
        /// Source-generated regex for parsing featuring artists from track titles.
        /// Matches variations like (feat. Artist), (ft. Artist), (featuring Artist).
        /// </summary>
        [GeneratedRegex(@"\([fF](?:ea)?t(?:uring)?\.? (.*?)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        internal static partial Regex FeaturingArtistParsing();
    }
}
