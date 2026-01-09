using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;

namespace FtoConsulting.PortfolioManager.Api.Authentication;

/// <summary>
/// Authentication handler for system-to-system API key authentication.
/// Used for internal service calls like scheduled jobs from Azure Functions.
/// </summary>
public class SystemApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SystemApiKey";
    public const string ApiKeyHeaderName = "X-System-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if the API key header is present
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key header not found."));
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key header is empty."));
        }

        // Get the expected API key from configuration
        // Supports both "SystemApiKey" (Key Vault direct) and "SystemApi:ApiKey" (appsettings hierarchy)
        var expectedApiKey = configuration["SystemApiKey"] ?? configuration["SystemApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            Logger.LogError("SystemApi:ApiKey is not configured. System API endpoints are disabled.");
            return Task.FromResult(AuthenticateResult.Fail("System API is not configured."));
        }

        // Validate the API key using constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(providedApiKey),
            System.Text.Encoding.UTF8.GetBytes(expectedApiKey)))
        {
            Logger.LogWarning("Invalid system API key provided from {RemoteIp}", 
                Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key."));
        }

        // Create claims for the authenticated system caller
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "SystemService"),
            new Claim(ClaimTypes.Role, "System"),
            new Claim("caller_type", "scheduled_job")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("System API key authentication successful from {RemoteIp}", 
            Context.Connection.RemoteIpAddress);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
