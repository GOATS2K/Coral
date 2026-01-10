namespace Coral.Dto;

/// <summary>
/// Standard API error response.
/// </summary>
public record ApiError(string Error);

/// <summary>
/// Standard API error response with additional data.
/// </summary>
/// <typeparam name="T">The type of additional error data.</typeparam>
public record ApiError<T>(string Error, T Data);
