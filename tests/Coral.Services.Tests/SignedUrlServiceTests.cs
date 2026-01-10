using Coral.Configuration.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Coral.Services.Tests;

public class SignedUrlServiceTests
{
    private static readonly ServerConfiguration TestConfig = new()
    {
        Jwt = new JwtSettings { Secret = Convert.ToBase64String(new byte[32]) }
    };

    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));

    private ISignedUrlService CreateService(TimeProvider? timeProvider = null) =>
        new SignedUrlService(Options.Create(TestConfig), timeProvider ?? _timeProvider);

    [Fact]
    public void GenerateSignedUrl_SimplePath_ReturnsUrlWithQueryParams()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";

        // act
        var signedUrl = service.GenerateSignedUrl(path);

        // assert
        Assert.StartsWith(path, signedUrl);
        Assert.Contains("?expires=", signedUrl);
        Assert.Contains("&signature=", signedUrl);
    }

    [Fact]
    public void GenerateSignedUrl_PathWithQueryString_AppendsWithAmpersand()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream?quality=high";

        // act
        var signedUrl = service.GenerateSignedUrl(path);

        // assert
        Assert.StartsWith(path, signedUrl);
        Assert.Contains("&expires=", signedUrl);
        Assert.Contains("&signature=", signedUrl);
    }

    [Fact]
    public void GenerateSignedUrl_DefaultExpiry_ExpiresIn24Hours()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";

        // act
        var signedUrl = service.GenerateSignedUrl(path);

        // assert
        var expires = ExtractExpiresFromUrl(signedUrl);
        var expectedExpires = _timeProvider.GetUtcNow().AddHours(24).ToUnixTimeSeconds();
        Assert.Equal(expectedExpires, expires);
    }

    [Fact]
    public void GenerateSignedUrl_CustomExpiry_UsesProvidedExpiry()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";
        var expiry = TimeSpan.FromMinutes(5);

        // act
        var signedUrl = service.GenerateSignedUrl(path, expiry);

        // assert
        var expires = ExtractExpiresFromUrl(signedUrl);
        var expectedExpires = _timeProvider.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds();
        Assert.Equal(expectedExpires, expires);
    }

    [Fact]
    public void ValidateSignature_ValidSignature_ReturnsTrue()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";
        var signedUrl = service.GenerateSignedUrl(path);
        var (expires, signature) = ExtractParamsFromUrl(signedUrl);

        // act
        var isValid = service.ValidateSignature(path, expires, signature);

        // assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateSignature_TamperedPath_ReturnsFalse()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";
        var signedUrl = service.GenerateSignedUrl(path);
        var (expires, signature) = ExtractParamsFromUrl(signedUrl);

        var tamperedPath = "/api/audio/456/stream";

        // act
        var isValid = service.ValidateSignature(tamperedPath, expires, signature);

        // assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_TamperedSignature_ReturnsFalse()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";
        var signedUrl = service.GenerateSignedUrl(path);
        var (expires, _) = ExtractParamsFromUrl(signedUrl);

        var tamperedSignature = "tampered_signature_value";

        // act
        var isValid = service.ValidateSignature(path, expires, tamperedSignature);

        // assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_ExpiredSignature_ReturnsFalse()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";
        var signedUrl = service.GenerateSignedUrl(path, TimeSpan.FromMinutes(5));
        var (expires, signature) = ExtractParamsFromUrl(signedUrl);

        // Advance time past expiry
        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        // act
        var isValid = service.ValidateSignature(path, expires, signature);

        // assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_NotYetExpired_ReturnsTrue()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";
        var signedUrl = service.GenerateSignedUrl(path, TimeSpan.FromMinutes(10));
        var (expires, signature) = ExtractParamsFromUrl(signedUrl);

        // Advance time but not past expiry
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // act
        var isValid = service.ValidateSignature(path, expires, signature);

        // assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateSignature_DifferentSecrets_ReturnsFalse()
    {
        // arrange
        var path = "/api/audio/123/stream";

        var config1 = new ServerConfiguration
        {
            Jwt = new JwtSettings { Secret = Convert.ToBase64String(new byte[32]) }
        };
        var config2 = new ServerConfiguration
        {
            Jwt = new JwtSettings { Secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 }) }
        };

        var service1 = new SignedUrlService(Options.Create(config1), _timeProvider);
        var service2 = new SignedUrlService(Options.Create(config2), _timeProvider);

        var signedUrl = service1.GenerateSignedUrl(path);
        var (expires, signature) = ExtractParamsFromUrl(signedUrl);

        // act
        var isValid = service2.ValidateSignature(path, expires, signature);

        // assert
        Assert.False(isValid);
    }

    [Fact]
    public void GenerateSignedUrl_ProducesUrlSafeSignature()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";

        // act
        var signedUrl = service.GenerateSignedUrl(path);
        var (_, signature) = ExtractParamsFromUrl(signedUrl);

        // assert - signature should not contain URL-unsafe characters
        Assert.DoesNotContain("+", signature);
        Assert.DoesNotContain("/", signature);
        Assert.DoesNotContain("=", signature);
    }

    [Fact]
    public void GenerateSignedUrl_DifferentTimes_ProducesDifferentSignatures()
    {
        // arrange
        var service = CreateService();
        var path = "/api/audio/123/stream";

        // act
        var signedUrl1 = service.GenerateSignedUrl(path);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        var signedUrl2 = service.GenerateSignedUrl(path);

        // assert
        Assert.NotEqual(signedUrl1, signedUrl2);
    }

    [Theory]
    [InlineData("/api/audio/1/stream")]
    [InlineData("/api/audio/999/stream")]
    [InlineData("/api/track/abc-def/file")]
    [InlineData("/path/with spaces/file")]
    [InlineData("/path?existing=query")]
    public void ValidateSignature_VariousPaths_ValidatesOwnSignatures(string path)
    {
        // arrange
        var service = CreateService();

        // act
        var signedUrl = service.GenerateSignedUrl(path);
        var (expires, signature) = ExtractParamsFromUrl(signedUrl);
        var isValid = service.ValidateSignature(path, expires, signature);

        // assert
        Assert.True(isValid);
    }

    private static long ExtractExpiresFromUrl(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(url, @"expires=(\d+)");
        return long.Parse(match.Groups[1].Value);
    }

    private static (long expires, string signature) ExtractParamsFromUrl(string url)
    {
        var expiresMatch = System.Text.RegularExpressions.Regex.Match(url, @"expires=(\d+)");
        var signatureMatch = System.Text.RegularExpressions.Regex.Match(url, @"signature=([^&]+)");

        return (long.Parse(expiresMatch.Groups[1].Value), signatureMatch.Groups[1].Value);
    }
}
