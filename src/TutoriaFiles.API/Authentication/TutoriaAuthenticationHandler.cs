using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using TutoriaFiles.Core.Interfaces;

namespace TutoriaFiles.API.Authentication;

/// <summary>
/// Custom authentication handler that validates JWT tokens by calling TutoriaApi first,
/// with fallback to local validation if TutoriaApi is unavailable.
/// This ensures JWT secrets are only maintained in one place (TutoriaApi).
/// </summary>
public class TutoriaAuthenticationHandler : AuthenticationHandler<JwtBearerOptions>
{
    private readonly ITokenValidationService _tokenValidationService;

    public TutoriaAuthenticationHandler(
        IOptionsMonitor<JwtBearerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenValidationService tokenValidationService)
        : base(options, logger, encoder)
    {
        _tokenValidationService = tokenValidationService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for Authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.NoResult();
        }

        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Invalid authorization header");
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();

        try
        {
            // Validate token using TutoriaApi + local fallback
            var principal = await _tokenValidationService.ValidateTokenAsync(token);

            if (principal == null)
            {
                return AuthenticateResult.Fail("Invalid or expired token");
            }

            // Create authentication ticket
            var ticket = new AuthenticationTicket(principal, JwtBearerDefaults.AuthenticationScheme);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during token validation");
            return AuthenticateResult.Fail("Token validation failed");
        }
    }
}
