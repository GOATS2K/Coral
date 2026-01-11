using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Coral.Configuration;
using Coral.Configuration.Models;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Coral.Services;

public interface IAuthService
{
    Task<AuthResult?> LoginAsync(LoginRequest request);
    Task<AuthResult?> RegisterAsync(RegisterRequest request);
    Task<SessionValidationResult> ValidateAndExtendSessionAsync(Guid deviceId, Guid tokenId);
    Task LogoutAsync(Guid deviceId);
    string GenerateToken(User user, Device device);
}

public class AuthService : IAuthService
{
    private readonly CoralDbContext _context;
    private readonly IUserService _userService;
    private readonly JwtSettings _jwtSettings;
    private readonly ISessionCacheService _sessionCache;

    public AuthService(
        CoralDbContext context,
        IUserService userService,
        IOptions<ServerConfiguration> config,
        ISessionCacheService sessionCache)
    {
        _context = context;
        _userService = userService;
        _jwtSettings = config.Value.Jwt;
        _sessionCache = sessionCache;
    }

    public async Task<AuthResult?> LoginAsync(LoginRequest request)
    {
        var user = await _userService.GetUserByUsernameAsync(request.Username);
        if (user == null)
            return null;

        var result = _userService.ValidatePassword(user, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        var device = await GetOrCreateDeviceAsync(user.Id, request.Device, request.DeviceId);
        var token = GenerateToken(user, device);

        return new AuthResult(user, device, token);
    }

    public async Task<AuthResult?> RegisterAsync(RegisterRequest request)
    {
        // Check if username already exists
        var existingUser = await _userService.GetUserByUsernameAsync(request.Username);
        if (existingUser != null)
            return null;

        var user = await _userService.CreateUserAsync(request.Username, request.Password);
        var device = await GetOrCreateDeviceAsync(user.Id, request.Device, null);
        var token = GenerateToken(user, device);

        return new AuthResult(user, device, token);
    }

    public async Task<SessionValidationResult> ValidateAndExtendSessionAsync(Guid deviceId, Guid tokenId)
    {
        // Check cache first
        var cachedSession = _sessionCache.GetSession(deviceId);
        if (cachedSession != null)
            return await ValidateFromCache(cachedSession, deviceId, tokenId);

        return await ValidateFromDatabase(deviceId, tokenId);
    }

    private async Task<SessionValidationResult> ValidateFromCache(CachedSession cachedSession, Guid deviceId, Guid tokenId)
    {
        // Cached session is invalid - tokenId mismatch or expired
        if (cachedSession.TokenId != tokenId || cachedSession.SessionExpiresAt < DateTime.UtcNow)
        {
            _sessionCache.InvalidateSession(deviceId);
            return new SessionValidationResult(false, false);
        }

        var daysRemaining = (cachedSession.SessionExpiresAt - DateTime.UtcNow).TotalDays;
        var needsExtension = daysRemaining < 7;
        var needsLastSeenUpdate = _sessionCache.ShouldUpdateLastSeen(cachedSession);

        // No DB update needed - return immediately
        if (!needsLastSeenUpdate && !needsExtension)
            return new SessionValidationResult(true, false);

        // Needs DB update - fall through to database validation
        return await ValidateFromDatabase(deviceId, tokenId);
    }

    private async Task<SessionValidationResult> ValidateFromDatabase(Guid deviceId, Guid tokenId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null || device.TokenId != tokenId || device.SessionExpiresAt == null || device.SessionExpiresAt < DateTime.UtcNow)
        {
            _sessionCache.InvalidateSession(deviceId);
            return new SessionValidationResult(false, false);
        }

        device.LastSeenAt = DateTime.UtcNow;

        var daysRemaining = (device.SessionExpiresAt.Value - DateTime.UtcNow).TotalDays;
        var extended = daysRemaining < 7;
        if (extended)
            device.SessionExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.SessionExpirationDays);

        await _context.SaveChangesAsync();

        _sessionCache.SetSession(deviceId, device.TokenId.Value, device.SessionExpiresAt.Value, device.LastSeenAt);

        return new SessionValidationResult(true, extended);
    }

    public async Task LogoutAsync(Guid deviceId)
    {
        _sessionCache.InvalidateSession(deviceId);

        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            return;

        device.TokenId = null;
        device.SessionExpiresAt = null;
        await _context.SaveChangesAsync();
    }

    public string GenerateToken(User user, Device device)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Convert.FromBase64String(_jwtSettings.Secret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, device.TokenId!.Value.ToString()),
                new Claim(AuthConstants.ClaimTypes.Role, user.Role.ToString()),
                new Claim(AuthConstants.ClaimTypes.DeviceId, device.Id.ToString())
            ]),
            // No expiry - session validity is controlled by the database
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private async Task<Device> GetOrCreateDeviceAsync(Guid userId, DeviceInfo info, Guid? existingDeviceId)
    {
        Device? device = null;

        if (existingDeviceId.HasValue)
        {
            device = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == existingDeviceId.Value && d.UserId == userId);
        }

        if (device == null)
        {
            device = new Device
            {
                UserId = userId,
                Name = info.Name,
                Type = info.Type,
                OS = info.OS
            };
            _context.Devices.Add(device);
        }
        else
        {
            // Update device info in case it changed
            device.Name = info.Name;
            device.Type = info.Type;
            device.OS = info.OS;
        }

        // Create new session
        device.TokenId = Guid.NewGuid();
        device.SessionExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.SessionExpirationDays);
        device.LastSeenAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return device;
    }
}
