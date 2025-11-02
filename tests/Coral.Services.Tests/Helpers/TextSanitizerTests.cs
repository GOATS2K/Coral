using Coral.Services.Helpers;
using Xunit;

namespace Coral.Services.Tests.Helpers
{
    public class TextSanitizerTests
    {
        [Fact]
        public void SanitizeForUtf8_NullInput_ReturnsNull()
        {
            // arrange
            string? input = null;

            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("Hello World! This is a test.", "Hello World! This is a test.")]
        [InlineData("100% 💯 Perfect!", "100% 💯 Perfect!")]
        [InlineData("Music 🎵 is life 💖 🎶", "Music 🎵 is life 💖 🎶")]
        [InlineData("Café résumé naïve Москва 东京 العربية", "Café résumé naïve Москва 东京 العربية")]
        [InlineData("Line 1\r\nLine 2\tTabbed", "Line 1\r\nLine 2\tTabbed")]
        public void SanitizeForUtf8_ValidText_RemainsUnchanged(string input, string expected)
        {
            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Text with\0null byte", "Text withnull byte")]
        [InlineData("Text\0with\0multiple\0null\0bytes\0", "Textwithmultiplenullbytes")]
        [InlineData(".:: 2017-07-01 - Calibre - Essential Mix ::.\r\n\r\nBelfast's Calibre delivers a moody\0", ".:: 2017-07-01 - Calibre - Essential Mix ::.\r\n\r\nBelfast's Calibre delivers a moody")]
        public void SanitizeForUtf8_NullBytes_RemovesNullBytes(string input, string expected)
        {
            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Equal(expected, result);
            Assert.False(result.Contains('\0'), "Result should not contain null bytes");
        }

        [Fact]
        public void SanitizeForUtf8_UnpairedHighSurrogate_RemovesSurrogate()
        {
            // arrange
            // Create string with unpaired high surrogate (0xD800-0xDBFF)
            var input = "Text" + (char)0xD800 + "more text";

            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Equal("Textmore text", result);
        }

        [Fact]
        public void SanitizeForUtf8_UnpairedLowSurrogate_RemovesSurrogate()
        {
            // arrange
            // Create string with unpaired low surrogate (0xDC00-0xDFFF)
            // This is the issue found in "💯%" - emoji followed by unpaired low surrogate
            var input = "💯" + (char)0xDCAF + "%";

            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Equal("💯%", result);
            // Verify the emoji is preserved but unpaired surrogate is removed
            Assert.Contains("💯", result);
        }

        [Fact]
        public void SanitizeForUtf8_ValidSurrogatePair_PreservesPair()
        {
            // arrange
            // Create a valid surrogate pair (e.g., 😀 = U+1F600)
            var highSurrogate = (char)0xD83D;
            var lowSurrogate = (char)0xDE00;
            var input = "Text" + highSurrogate + lowSurrogate + "end";

            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Equal(input, result);
            Assert.Contains("😀", result);
        }

        [Fact]
        public void SanitizeForUtf8_MixedValidAndInvalidSurrogates_RemovesOnlyInvalid()
        {
            // arrange
            // Valid emoji + unpaired low surrogate + more text
            var input = "Valid 😀 " + (char)0xDC00 + " invalid";

            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Equal("Valid 😀  invalid", result);
            Assert.Contains("😀", result);
        }

        [Fact]
        public void SanitizeForUtf8_RealWorldExample_DJPaypal100Percent()
        {
            // arrange
            // Real-world example: "💯%" with unpaired surrogate
            // The actual corruption would be: emoji + unpaired low surrogate
            var input = "💯" + (char)0xDCAF + "%";

            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            // Should preserve the valid emoji but remove the unpaired surrogate
            Assert.Equal("💯%", result);
            Assert.Contains("💯", result);
            Assert.EndsWith("%", result);
        }

        [Fact]
        public void SanitizeForUtf8_ComplexMixture_HandlesCorrectly()
        {
            // arrange
            // Complex mixture of valid and invalid characters
            var input = "Track: 💯" + (char)0xDC00 + " Artist\0 - Album 🎵";

            // act
            var result = TextSanitizer.SanitizeForUtf8(input);

            // assert
            Assert.Equal("Track: 💯 Artist - Album 🎵", result);
            Assert.False(result.Contains('\0'), "Result should not contain null bytes");
            Assert.Contains("💯", result);
            Assert.Contains("🎵", result);
        }
    }
}
