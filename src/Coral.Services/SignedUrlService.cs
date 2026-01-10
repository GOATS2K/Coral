using System.Security.Cryptography;
using System.Text;
using Coral.Configuration.Models;
using Microsoft.Extensions.Options;

namespace Coral.Services;

public interface ISignedUrlService
{
    string GenerateSignedUrl(string path, TimeSpan? expiresIn = null);
    bool ValidateSignature(string path, long expires, string signature);
}

public class SignedUrlService : ISignedUrlService
{
    private readonly byte[] _secretBytes;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(24);

    public SignedUrlService(IOptions<ServerConfiguration> config)
    {
        _secretBytes = Encoding.UTF8.GetBytes(config.Value.Jwt.Secret);
    }

    public string GenerateSignedUrl(string path, TimeSpan? expiresIn = null)
    {
        var expires = DateTimeOffset.UtcNow.Add(expiresIn ?? _defaultExpiry).ToUnixTimeSeconds();
        var signature = ComputeSignature(path, expires);

        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}expires={expires}&signature={signature}";
    }

    public bool ValidateSignature(string path, long expires, string signature)
    {
        // Check if expired
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expires);
        if (expiresAt < DateTimeOffset.UtcNow)
            return false;

        // Validate signature
        var expectedSignature = ComputeSignature(path, expires);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature)
        );
    }

    private string ComputeSignature(string path, long expires)
    {
        var dataToSign = $"{path}{expires}";
        using var hmac = new HMACSHA256(_secretBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
