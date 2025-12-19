using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TutoriaFiles.Core.Interfaces;

namespace TutoriaFiles.Infrastructure.Services;

public class TokenValidationService : ITokenValidationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenValidationService> _logger;
    private readonly string? _tutoriaApiUrl;
    private readonly string? _jwtSecret;
    private readonly string? _jwtIssuer;
    private readonly string? _jwtAudience;

    public TokenValidationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TokenValidationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        _tutoriaApiUrl = configuration["TutoriaApi:BaseUrl"];
        _jwtSecret = configuration["Jwt:SecretKey"];
        _jwtIssuer = configuration["Jwt:Issuer"];
        _jwtAudience = configuration["Jwt:Audience"];
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        // Strategy 1: Call TutoriaApi to validate (single source of truth)
        if (!string.IsNullOrWhiteSpace(_tutoriaApiUrl))
        {
            try
            {
                var principal = await ValidateTokenViaTutoriaApiAsync(token);
                if (principal != null)
                {
                    _logger.LogDebug("Token validated successfully via TutoriaApi");
                    return principal;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate token via TutoriaApi, falling back to local validation");
            }
        }
        else
        {
            _logger.LogDebug("TutoriaApi URL not configured, using local validation");
        }

        // Strategy 2: Fallback to local validation
        var localPrincipal = ValidateTokenLocally(token);
        if (localPrincipal != null)
        {
            _logger.LogDebug("Token validated successfully via local validation (fallback)");
        }
        else
        {
            _logger.LogWarning("Token validation failed both via TutoriaApi and locally");
        }

        return localPrincipal;
    }

    private async Task<ClaimsPrincipal?> ValidateTokenViaTutoriaApiAsync(string token)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5); // 5 second timeout

        var request = new HttpRequestMessage(HttpMethod.Get, $"{_tutoriaApiUrl}/api/auth/validate-token");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogDebug("TutoriaApi returned 401 Unauthorized");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TutoriaApi returned {StatusCode}", response.StatusCode);
            return null;
        }

        // TutoriaApi returns the token payload on success
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        if (payload == null)
        {
            _logger.LogWarning("TutoriaApi returned empty payload");
            return null;
        }

        // Convert the payload to ClaimsPrincipal
        return CreatePrincipalFromPayload(payload);
    }

    public ClaimsPrincipal? ValidateTokenLocally(string token)
    {
        if (string.IsNullOrWhiteSpace(_jwtSecret))
        {
            _logger.LogError("JWT SecretKey not configured - cannot validate locally");
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSecret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = !string.IsNullOrWhiteSpace(_jwtIssuer),
                ValidIssuer = _jwtIssuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(_jwtAudience),
                ValidAudience = _jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogDebug(ex, "Local token validation failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during local token validation");
            return null;
        }
    }

    private ClaimsPrincipal CreatePrincipalFromPayload(Dictionary<string, object> payload)
    {
        var claims = new List<Claim>();

        foreach (var kvp in payload)
        {
            var value = kvp.Value?.ToString();
            if (value != null)
            {
                // Map common JWT claims to standard ClaimTypes
                var claimType = kvp.Key switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "name" => ClaimTypes.Name,
                    "email" => ClaimTypes.Email,
                    "role" => ClaimTypes.Role,
                    _ => kvp.Key
                };

                claims.Add(new Claim(claimType, value));
            }
        }

        var identity = new ClaimsIdentity(claims, "TutoriaApi");
        return new ClaimsPrincipal(identity);
    }
}
