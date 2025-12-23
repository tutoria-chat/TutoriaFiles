using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaFiles.Core.Interfaces;

namespace TutoriaFiles.Infrastructure.Services;

/// <summary>
/// Validates JWT tokens by calling TutoriaApi's validation endpoint.
/// This ensures the JWT secret only needs to be maintained in one place (TutoriaApi).
/// </summary>
public class TokenValidationService : ITokenValidationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenValidationService> _logger;
    private readonly string _tutoriaApiUrl;

    public TokenValidationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TokenValidationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _tutoriaApiUrl = configuration["TutoriaApi:BaseUrl"]
            ?? throw new InvalidOperationException("TutoriaApi:BaseUrl must be configured for token validation");

        _logger.LogInformation("[TokenValidation] Configured to validate against: {Url}", _tutoriaApiUrl);
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        var validationUrl = $"{_tutoriaApiUrl}/api/auth/validate-token";
        _logger.LogDebug("[TokenValidation] Validating token against: {Url}", validationUrl);

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        var request = new HttpRequestMessage(HttpMethod.Get, validationUrl);
        request.Headers.Add("Authorization", $"Bearer {token}");

        try
        {
            var response = await client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogDebug("[TokenValidation] Token invalid or expired (401)");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[TokenValidation] TutoriaApi returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            if (payload == null)
            {
                _logger.LogWarning("[TokenValidation] Empty payload from TutoriaApi");
                return null;
            }

            _logger.LogDebug("[TokenValidation] Token valid, claims: {Claims}", string.Join(", ", payload.Keys));
            return CreatePrincipalFromPayload(payload);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TokenValidation] Failed to reach TutoriaApi: {Message}", ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("[TokenValidation] Timeout calling TutoriaApi (5s limit)");
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
