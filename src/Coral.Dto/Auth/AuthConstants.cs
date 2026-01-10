namespace Coral.Dto.Auth;

/// <summary>
/// Authentication-related constants for strongly-typed claim names and schemes.
/// </summary>
public static class AuthConstants
{
    /// <summary>
    /// Custom claim type names used in Coral authentication.
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// The device ID claim. Identifies the device associated with the session.
        /// </summary>
        public const string DeviceId = "device_id";

        /// <summary>
        /// The token ID claim. Used to validate sessions against the database.
        /// For JWT: stored in the standard 'jti' claim.
        /// For cookies: stored as a custom claim.
        /// </summary>
        public const string TokenId = "token_id";

        /// <summary>
        /// The role claim for authorization purposes.
        /// </summary>
        public const string Role = "role";
    }

    /// <summary>
    /// Authentication scheme names.
    /// </summary>
    public static class Schemes
    {
        /// <summary>
        /// The composite policy scheme that selects between Cookie and JWT.
        /// </summary>
        public const string CoralAuth = "CoralAuth";
    }

    /// <summary>
    /// Cookie names used in authentication.
    /// </summary>
    public static class Cookies
    {
        /// <summary>
        /// The authentication cookie name.
        /// </summary>
        public const string AuthCookie = "coral_auth";
    }
}
