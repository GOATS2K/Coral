using Coral.Database.Models;
using OS = Coral.Database.Models.OperatingSystem;

namespace Coral.Dto.Auth;

// Request DTOs
public record LoginRequest(
    string Username,
    string Password,
    DeviceInfo Device,
    Guid? DeviceId = null
);

public record RegisterRequest(
    string Username,
    string Password,
    DeviceInfo Device
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record DeviceInfo(
    string Name,
    DeviceType Type,
    OS OS
);

// Response DTOs
public record LoginResponse(
    string? AccessToken,  // null for web (cookie is set)
    Guid DeviceId,
    UserDto User
);

public record AuthStatusResponse(
    bool RequiresSetup,
    bool IsAuthenticated
);

// Entity DTOs
public record UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = null!;
    public UserRole Role { get; init; }
}

public record DeviceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public DeviceType Type { get; init; }
    public OS OS { get; init; }
    public DateTime LastSeenAt { get; init; }
    public bool HasActiveSession { get; init; }
    public bool IsCurrent { get; init; }
}

// Internal service result records (replaces tuples)
public record AuthResult(
    User User,
    Device Device,
    string Token
);

public record SessionValidationResult(
    bool IsValid,
    bool Extended
);
