namespace Coral.Api.Attributes;

/// <summary>
/// Marks an endpoint to skip session validation.
/// Use this for endpoints that need to work regardless of session state (e.g., auth status check).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class SkipSessionValidationAttribute : Attribute
{
}
