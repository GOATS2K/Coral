using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
using Coral.Dto;
using Coral.Dto.Auth;
using Coral.Services;
using Coral.Services.Exceptions;
using Coral.Api.Attributes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public AuthController(IAuthService authService, IUserService userService, IMapper mapper)
    {
        _authService = authService;
        _userService = userService;
        _mapper = mapper;
    }

    [HttpGet("status")]
    [SkipSessionValidation]
    public async Task<ActionResult<AuthStatusResponse>> GetStatus()
    {
        var requiresSetup = await _userService.IsFirstUserAsync();

        // Check if user has a valid session (not just a valid cookie)
        var isAuthenticated = false;
        if (User.Identity?.IsAuthenticated != true) 
            return Ok(new AuthStatusResponse(requiresSetup, isAuthenticated));
        
        var deviceIdClaim = User.FindFirst(AuthConstants.ClaimTypes.DeviceId)?.Value;
        var tokenIdClaim = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                           ?? User.FindFirst(AuthConstants.ClaimTypes.TokenId)?.Value;

        if (!Guid.TryParse(deviceIdClaim, out var deviceId) ||
            !Guid.TryParse(tokenIdClaim, out var tokenId))
            return Ok(new AuthStatusResponse(requiresSetup, isAuthenticated));
        
        var result = await _authService.ValidateAndExtendSessionAsync(deviceId, tokenId);
        isAuthenticated = result.IsValid;

        return Ok(new AuthStatusResponse(requiresSetup, isAuthenticated));
    }

    [HttpPost("register")]
    [SkipSessionValidation]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        // Only allow registration if no users exist or the request is authenticated
        var isFirstUser = await _userService.IsFirstUserAsync();
        if (!isFirstUser && User.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        AuthResult? result;
        try
        {
            result = await _authService.RegisterAsync(request);
        }
        catch (InvalidUsernameException)
        {
            return BadRequest(new ApiError("Username must contain only letters, numbers, dashes, and underscores"));
        }

        if (result == null)
        {
            return BadRequest(new ApiError("Username already exists"));
        }

        var userDto = _mapper.Map<UserDto>(result.User);

        // For web clients, use cookie authentication
        if (IsWebClient(request.Device))
        {
            await SignInWithCookieAsync(result);
            return Ok(new LoginResponse(null, result.Device.Id, userDto));
        }

        // For native clients, return JWT in body
        return Ok(new LoginResponse(result.Token, result.Device.Id, userDto));
    }

    [HttpPost("login")]
    [SkipSessionValidation]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null)
        {
            return Unauthorized(new ApiError("Invalid username or password"));
        }

        var userDto = _mapper.Map<UserDto>(result.User);

        // For web clients, use cookie authentication
        if (IsWebClient(request.Device))
        {
            await SignInWithCookieAsync(result);
            return Ok(new LoginResponse(null, result.Device.Id, userDto));
        }

        // For native clients, return JWT in body
        return Ok(new LoginResponse(result.Token, result.Device.Id, userDto));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var deviceIdClaim = User.FindFirst(AuthConstants.ClaimTypes.DeviceId)?.Value;
        if (Guid.TryParse(deviceIdClaim, out var deviceId))
        {
            await _authService.LogoutAsync(deviceId);
        }

        // Sign out from cookie authentication
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        // Try standard JWT claim first, fall back to ClaimTypes.NameIdentifier for cookie auth
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(_mapper.Map<UserDto>(user));
    }

    [HttpPost("changePassword")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var success = await _userService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        if (!success)
        {
            return BadRequest(new ApiError("Current password is incorrect"));
        }

        return Ok();
    }

    private bool IsWebClient(DeviceInfo device)
    {
        // Web and Electron both run in browser context and use cookies
        return device.Type is Database.Models.DeviceType.Web or Database.Models.DeviceType.Electron;
    }

    private async Task SignInWithCookieAsync(AuthResult authResult)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authResult.User.Id.ToString()),
            new(ClaimTypes.Name, authResult.User.Username),
            new(ClaimTypes.Role, authResult.User.Role.ToString()),
            new(AuthConstants.ClaimTypes.DeviceId, authResult.Device.Id.ToString()),
            new(AuthConstants.ClaimTypes.TokenId, authResult.Device.TokenId!.Value.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = authResult.Device.SessionExpiresAt
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);
    }
}
