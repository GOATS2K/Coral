using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
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
    private readonly IMapper _mapper;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        CoralDbContext context,
        IUserService userService,
        IMapper mapper,
        IOptions<ServerConfiguration> config)
    {
        _context = context;
        _userService = userService;
        _mapper = mapper;
        _jwtSettings = config.Value.Jwt;
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
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null || device.TokenId != tokenId)
            return new SessionValidationResult(false, false);

        if (device.SessionExpiresAt == null || device.SessionExpiresAt < DateTime.UtcNow)
            return new SessionValidationResult(false, false);

        // Update last seen
        device.LastSeenAt = DateTime.UtcNow;

        // Sliding expiration: if less than 7 days remaining, extend
        var daysRemaining = (device.SessionExpiresAt.Value - DateTime.UtcNow).TotalDays;
        var extended = false;
        if (daysRemaining < 7)
        {
            device.SessionExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.SessionExpirationDays);
            extended = true;
        }

        await _context.SaveChangesAsync();
        return new SessionValidationResult(true, extended);
    }

    public async Task LogoutAsync(Guid deviceId)
    {
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
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, device.TokenId!.Value.ToString()),
                new Claim(AuthConstants.ClaimTypes.Role, user.Role.ToString()),
                new Claim(AuthConstants.ClaimTypes.DeviceId, device.Id.ToString())
            }),
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
