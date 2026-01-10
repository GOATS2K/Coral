using System.IdentityModel.Tokens.Jwt;
using Coral.Api.Attributes;
using Coral.Dto;
using Coral.Dto.Auth;
using Coral.Services;

namespace Coral.Api.Middleware;

public class SessionAuthMiddleware
{
    private readonly RequestDelegate _next;

    public SessionAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        // Skip validation for endpoints marked with [SkipSessionValidation]
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<SkipSessionValidationAttribute>() != null)
        {
            await _next(context);
            return;
        }

        // Skip validation for unauthenticated requests
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var deviceIdClaim = context.User.FindFirst(AuthConstants.ClaimTypes.DeviceId)?.Value;
        // Try standard JWT claim first (jti), then fall back to custom claim for cookie auth
        var tokenIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
            ?? context.User.FindFirst(AuthConstants.ClaimTypes.TokenId)?.Value;

        // Reject if session claims are missing or invalid
        if (!Guid.TryParse(deviceIdClaim, out var deviceId) ||
            !Guid.TryParse(tokenIdClaim, out var tokenId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ApiError("Invalid session claims"));
            return;
        }

        var result = await authService.ValidateAndExtendSessionAsync(deviceId, tokenId);
        if (!result.IsValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ApiError("Session expired or revoked"));
            return;
        }

        // Session valid - continue with request
        // Note: If session was extended, the same token continues to work since we check session in DB
        await _next(context);
    }
}

public static class SessionAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionAuthMiddleware>();
    }
}
