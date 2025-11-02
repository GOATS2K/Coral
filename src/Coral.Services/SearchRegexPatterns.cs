using System.Text.RegularExpressions;

namespace Coral.Services
{
    /// <summary>
    /// Contains source-generated regex patterns for search operations.
    /// </summary>
    internal static partial class SearchRegexPatterns
    {
        /// <summary>
        /// Source-generated regex for extracting keywords from input strings.
        /// Matches Unicode letters (\p{L}) and decimal numbers (\p{Nd}).
        /// </summary>
        [GeneratedRegex(@"[\p{L}\p{Nd}]+", RegexOptions.IgnoreCase)]
        internal static partial Regex KeywordExtraction();
    }
}
