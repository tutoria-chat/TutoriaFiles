using System.Security.Claims;

namespace TutoriaFiles.Core.Interfaces;

/// <summary>
/// Validates JWT tokens by calling TutoriaApi's validation endpoint.
/// This ensures the JWT secret only needs to be maintained in one place.
/// </summary>
public interface ITokenValidationService
{
    /// <summary>
    /// Validates a JWT token by calling TutoriaApi's /api/auth/validate-token endpoint.
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
}
