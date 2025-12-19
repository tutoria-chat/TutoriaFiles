using System.Security.Claims;

namespace TutoriaFiles.Core.Interfaces;

/// <summary>
/// Service for validating JWT tokens by calling TutoriaApi with local fallback.
/// This ensures the JWT secret only needs to be maintained in one place (TutoriaApi).
/// </summary>
public interface ITokenValidationService
{
    /// <summary>
    /// Validates a JWT token by first calling TutoriaApi's validation endpoint.
    /// Falls back to local validation if TutoriaApi is unavailable.
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

    /// <summary>
    /// Validates a JWT token locally using the configured secret.
    /// Only used as fallback when TutoriaApi is unavailable.
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    ClaimsPrincipal? ValidateTokenLocally(string token);
}
