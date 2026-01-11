using System.IdentityModel.Tokens.Jwt;
using Coral.Configuration.Models;
using Coral.Database.Models;
using Coral.Dto.Auth;
using Coral.TestProviders;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using OS = Coral.Database.Models.OperatingSystem;

namespace Coral.Services.Tests;

public class AuthServiceTests(DatabaseFixture fixture) : TransactionTestBase(fixture)
{
    private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
    private readonly ISessionCacheService _sessionCache = new SessionCacheService(
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<SessionCacheService>.Instance);

    private static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = Convert.ToBase64String(new byte[32]), // 256-bit key
        SessionExpirationDays = 30
    };

    private IUserService UserService => new UserService(
        TestDatabase.Context,
        _passwordHasher);

    private IAuthService AuthService => new AuthService(
        TestDatabase.Context,
        UserService,
        Options.Create(new ServerConfiguration { Jwt = TestJwtSettings }),
        _sessionCache);

    private static DeviceInfo CreateDeviceInfo(string name = "Test Device") =>
        new(name, DeviceType.Web, OS.Windows);

    [Fact]
    public async Task RegisterAsync_NewUser_CreatesUserAndReturnsAuthResult()
    {
        // arrange
        var request = new RegisterRequest("newuser", "password123", CreateDeviceInfo());

        // act
        var result = await AuthService.RegisterAsync(request);

        // assert
        Assert.NotNull(result);
        Assert.Equal(request.Username, result.User.Username);
        Assert.NotNull(result.Device);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task RegisterAsync_ExistingUsername_ReturnsNull()
    {
        // arrange
        var username = "existinguser";
        await UserService.CreateUserAsync(username, "password123");

        var request = new RegisterRequest(username, "differentpassword", CreateDeviceInfo());

        // act
        var result = await AuthService.RegisterAsync(request);

        // assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResult()
    {
        // arrange
        var username = "loginuser";
        var password = "password123";
        await UserService.CreateUserAsync(username, password);

        var request = new LoginRequest(username, password, CreateDeviceInfo());

        // act
        var result = await AuthService.LoginAsync(request);

        // assert
        Assert.NotNull(result);
        Assert.Equal(username, result.User.Username);
        Assert.NotNull(result.Device);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task LoginAsync_InvalidUsername_ReturnsNull()
    {
        // arrange
        var request = new LoginRequest("nonexistent", "password123", CreateDeviceInfo());

        // act
        var result = await AuthService.LoginAsync(request);

        // assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsNull()
    {
        // arrange
        var username = "testuser";
        await UserService.CreateUserAsync(username, "correctpassword");

        var request = new LoginRequest(username, "wrongpassword", CreateDeviceInfo());

        // act
        var result = await AuthService.LoginAsync(request);

        // assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_ExistingDevice_ReusesDevice()
    {
        // arrange
        var username = "deviceuser";
        var password = "password123";
        await UserService.CreateUserAsync(username, password);

        var firstLogin = await AuthService.LoginAsync(
            new LoginRequest(username, password, CreateDeviceInfo("First Login")));

        // act
        var secondLogin = await AuthService.LoginAsync(
            new LoginRequest(username, password, CreateDeviceInfo("Second Login"), firstLogin!.Device.Id));

        // assert
        Assert.NotNull(secondLogin);
        Assert.Equal(firstLogin.Device.Id, secondLogin.Device.Id);
        Assert.Equal("Second Login", secondLogin.Device.Name); // Device info updated
    }

    [Fact]
    public async Task ValidateAndExtendSessionAsync_ValidSession_ReturnsValid()
    {
        // arrange
        var registerResult = await AuthService.RegisterAsync(
            new RegisterRequest("sessionuser", "password123", CreateDeviceInfo()));

        // act
        var result = await AuthService.ValidateAndExtendSessionAsync(
            registerResult!.Device.Id,
            registerResult.Device.TokenId!.Value);

        // assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAndExtendSessionAsync_InvalidDeviceId_ReturnsInvalid()
    {
        // arrange
        var nonExistentDeviceId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        // act
        var result = await AuthService.ValidateAndExtendSessionAsync(nonExistentDeviceId, tokenId);

        // assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAndExtendSessionAsync_WrongTokenId_ReturnsInvalid()
    {
        // arrange
        var registerResult = await AuthService.RegisterAsync(
            new RegisterRequest("tokenuser", "password123", CreateDeviceInfo()));

        var wrongTokenId = Guid.NewGuid();

        // act
        var result = await AuthService.ValidateAndExtendSessionAsync(
            registerResult!.Device.Id,
            wrongTokenId);

        // assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAndExtendSessionAsync_ExpiredSession_ReturnsInvalid()
    {
        // arrange
        var registerResult = await AuthService.RegisterAsync(
            new RegisterRequest("expireduser", "password123", CreateDeviceInfo()));

        // Manually expire the session
        var device = await TestDatabase.Context.Devices.FindAsync(registerResult!.Device.Id);
        device!.SessionExpiresAt = DateTime.UtcNow.AddDays(-1);
        await TestDatabase.Context.SaveChangesAsync();

        // act
        var result = await AuthService.ValidateAndExtendSessionAsync(
            registerResult.Device.Id,
            registerResult.Device.TokenId!.Value);

        // assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAndExtendSessionAsync_SessionNearExpiry_ExtendsSession()
    {
        // arrange
        var registerResult = await AuthService.RegisterAsync(
            new RegisterRequest("extenduser", "password123", CreateDeviceInfo()));

        // Set session to expire in 3 days (less than 7 day threshold)
        var device = await TestDatabase.Context.Devices.FindAsync(registerResult!.Device.Id);
        var originalExpiry = DateTime.UtcNow.AddDays(3);
        device!.SessionExpiresAt = originalExpiry;
        await TestDatabase.Context.SaveChangesAsync();

        // act
        var result = await AuthService.ValidateAndExtendSessionAsync(
            registerResult.Device.Id,
            registerResult.Device.TokenId!.Value);

        // assert
        Assert.True(result.IsValid);
        Assert.True(result.Extended);

        // Verify session was extended
        await TestDatabase.Context.Entry(device).ReloadAsync();
        Assert.True(device.SessionExpiresAt > originalExpiry);
    }

    [Fact]
    public async Task ValidateAndExtendSessionAsync_SessionNotNearExpiry_DoesNotExtend()
    {
        // arrange
        var registerResult = await AuthService.RegisterAsync(
            new RegisterRequest("noextenduser", "password123", CreateDeviceInfo()));

        // Session is set to 30 days by default, which is more than 7 days
        // act
        var result = await AuthService.ValidateAndExtendSessionAsync(
            registerResult!.Device.Id,
            registerResult.Device.TokenId!.Value);

        // assert
        Assert.True(result.IsValid);
        Assert.False(result.Extended);
    }

    [Fact]
    public async Task LogoutAsync_ValidDevice_ClearsSession()
    {
        // arrange
        var registerResult = await AuthService.RegisterAsync(
            new RegisterRequest("logoutuser", "password123", CreateDeviceInfo()));

        // act
        await AuthService.LogoutAsync(registerResult!.Device.Id);

        // assert
        var device = await TestDatabase.Context.Devices.FindAsync(registerResult.Device.Id);
        Assert.Null(device!.TokenId);
        Assert.Null(device.SessionExpiresAt);
    }

    [Fact]
    public async Task LogoutAsync_NonExistentDevice_DoesNotThrow()
    {
        // arrange
        var nonExistentDeviceId = Guid.NewGuid();

        // act & assert - should not throw
        await AuthService.LogoutAsync(nonExistentDeviceId);
    }

    [Fact]
    public async Task GenerateToken_ValidUserAndDevice_ReturnsValidJwt()
    {
        // arrange
        var user = await UserService.CreateUserAsync("jwtuser", "password123");
        var device = new Device
        {
            UserId = user.Id,
            Name = "Test Device",
            Type = DeviceType.Web,
            OS = OS.Windows,
            TokenId = Guid.NewGuid(),
            SessionExpiresAt = DateTime.UtcNow.AddDays(30),
            LastSeenAt = DateTime.UtcNow
        };
        TestDatabase.Context.Devices.Add(device);
        await TestDatabase.Context.SaveChangesAsync();

        // act
        var token = AuthService.GenerateToken(user, device);

        // assert
        Assert.NotEmpty(token);

        // Verify token can be decoded
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwtToken.Subject);
        Assert.Equal(user.Username, jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Name).Value);
        Assert.Equal(device.TokenId.ToString(), jwtToken.Id);
        Assert.Equal(device.Id.ToString(), jwtToken.Claims.First(c => c.Type == AuthConstants.ClaimTypes.DeviceId).Value);
        Assert.Equal(user.Role.ToString(), jwtToken.Claims.First(c => c.Type == AuthConstants.ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task GenerateToken_HasExpectedClaims()
    {
        // arrange
        var user = await UserService.CreateUserAsync("claimsuser", "password123");
        var device = new Device
        {
            UserId = user.Id,
            Name = "Test Device",
            Type = DeviceType.Web,
            OS = OS.Windows,
            TokenId = Guid.NewGuid(),
            SessionExpiresAt = DateTime.UtcNow.AddDays(30),
            LastSeenAt = DateTime.UtcNow
        };
        TestDatabase.Context.Devices.Add(device);
        await TestDatabase.Context.SaveChangesAsync();

        // act
        var token = AuthService.GenerateToken(user, device);

        // assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Verify all expected claims are present
        var claims = jwtToken.Claims.ToList();
        Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Name && c.Value == user.Username);
        Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Jti && c.Value == device.TokenId.ToString());
        Assert.Contains(claims, c => c.Type == AuthConstants.ClaimTypes.DeviceId && c.Value == device.Id.ToString());
        Assert.Contains(claims, c => c.Type == AuthConstants.ClaimTypes.Role && c.Value == user.Role.ToString());
    }
}
