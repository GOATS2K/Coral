using System.Text;

namespace Coral.Services.Helpers;

/// <summary>
/// Provides utilities for sanitizing text fields to ensure PostgreSQL UTF-8 compatibility.
/// </summary>
public static class TextSanitizer
{
    /// <summary>
    /// Sanitizes a string to be PostgreSQL UTF-8 compatible while preserving valid content.
    /// Removes null bytes and unpaired surrogates, but keeps valid emoji and Unicode characters.
    /// </summary>
    /// <param name="text">The text to sanitize</param>
    /// <returns>Sanitized text safe for PostgreSQL UTF-8 storage</returns>
    public static string? SanitizeForUtf8(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Fast path: check if sanitization is even needed
        if (!text.Contains('\0') && !HasUnpairedSurrogates(text))
            return text;

        // Slow path: sanitize the text
        // First, remove null bytes - simple string replacement
        text = text.Replace("\0", "");

        // Now handle unpaired surrogates
        var cleaned = new StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Handle high surrogate
            if (char.IsHighSurrogate(c))
            {
                // Check if it's paired with a valid low surrogate
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    // Valid surrogate pair (emoji, etc.) - keep both
                    cleaned.Append(c);
                    cleaned.Append(text[i + 1]);
                    i++; // Skip the low surrogate in next iteration
                }
                // else: unpaired high surrogate - skip it (corrupted)
            }
            // Handle unpaired low surrogate
            else if (char.IsLowSurrogate(c))
            {
                // Unpaired low surrogate - skip it (corrupted)
                // Valid low surrogates are already handled in the high surrogate case above
            }
            else
            {
                // Regular character - keep it
                cleaned.Append(c);
            }
        }

        return cleaned.ToString();
    }

    /// <summary>
    /// Checks if a string contains unpaired surrogates that would be invalid in UTF-8.
    /// </summary>
    /// <param name="text">The text to check</param>
    /// <returns>True if the string contains unpaired surrogates, false otherwise</returns>
    private static bool HasUnpairedSurrogates(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsHighSurrogate(c))
            {
                // Check if it's paired with a valid low surrogate
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return true; // Unpaired high surrogate
                }
                i++; // Skip the low surrogate
            }
            else if (char.IsLowSurrogate(c))
            {
                return true; // Unpaired low surrogate (valid pairs already handled above)
            }
        }

        return false;
    }

    /// <summary>
    /// Sanitizes all text properties of a Track object for UTF-8 compatibility.
    /// </summary>
    public static void SanitizeTrackMetadata(ATL.Track track)
    {
        // Note: ATL.Track properties are read-only, so we can't modify them directly.
        // This method is for reference - actual sanitization should happen after
        // reading the track data and before storing in the database.
    }
}
