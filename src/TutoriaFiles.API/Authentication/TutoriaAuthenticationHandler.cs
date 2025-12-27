using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TutoriaFiles.Core.Interfaces;

namespace TutoriaFiles.API.Authentication;

public class TutoriaAuthenticationHandler : AuthenticationHandler<TutoriaAuthOptions>
{
    private readonly ITokenValidationService _tokenValidationService;
    private readonly ILogger<TutoriaAuthenticationHandler> _logger;

    public TutoriaAuthenticationHandler(
        IOptionsMonitor<TutoriaAuthOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ITokenValidationService tokenValidationService)
        : base(options, loggerFactory, encoder)
    {
        _tokenValidationService = tokenValidationService;
        _logger = loggerFactory.CreateLogger<TutoriaAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var path = Request.Path.ToString();
        _logger.LogInformation("[Auth] Authenticating request to {Path}", path);

        if (!Request.Headers.ContainsKey("Authorization"))
        {
            _logger.LogWarning("[Auth] No Authorization header for {Path}", path);
            return AuthenticateResult.NoResult();
        }

        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[Auth] Invalid auth header format for {Path}", path);
            return AuthenticateResult.Fail("Invalid authorization header");
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var tokenPreview = token.Length > 20 ? token[..20] + "..." : token;
        _logger.LogInformation("[Auth] Validating token {TokenPreview} for {Path}", tokenPreview, path);

        try
        {
            var principal = await _tokenValidationService.ValidateTokenAsync(token);

            if (principal == null)
            {
                _logger.LogWarning("[Auth] Token validation returned null for {Path}", path);
                return AuthenticateResult.Fail("Invalid or expired token");
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";
            _logger.LogInformation("[Auth] Token valid for user {UserId} role {Role} on {Path}", userId, role, path);

            var ticket = new AuthenticationTicket(principal, TutoriaAuthOptions.SchemeName);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Auth] Exception validating token for {Path}: {Message}", path, ex.Message);
            return AuthenticateResult.Fail("Token validation failed");
        }
    }
}
