using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using System.Globalization;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

// Configure culture for consistent date parsing
var cultureInfo = new CultureInfo("en-GB"); // UK culture for dd/MM/yyyy preference
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault if configured (for production)
var keyVaultName = builder.Configuration["KeyVault:Name"];
if (!string.IsNullOrEmpty(keyVaultName))
{
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}

// Add environment variables to configuration with mapping
builder.Configuration.AddEnvironmentVariables();

// Override Azure AD configuration with environment variables if available
var azureAdConfig = builder.Configuration.GetSection("AzureAd");
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_AD_INSTANCE")))
{
    azureAdConfig["Instance"] = Environment.GetEnvironmentVariable("AZURE_AD_INSTANCE");
}
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_AD_DOMAIN")))
{
    azureAdConfig["Domain"] = Environment.GetEnvironmentVariable("AZURE_AD_DOMAIN");
}
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_AD_TENANT_ID")))
{
    azureAdConfig["TenantId"] = Environment.GetEnvironmentVariable("AZURE_AD_TENANT_ID");
}
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_AD_CLIENT_ID")))
{
    azureAdConfig["ClientId"] = Environment.GetEnvironmentVariable("AZURE_AD_CLIENT_ID");
}
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_AD_AUDIENCE")))
{
    azureAdConfig["Audience"] = Environment.GetEnvironmentVariable("AZURE_AD_AUDIENCE");
}

// Configure structured logging and OpenTelemetry integration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

// Add OpenTelemetry logging provider
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("PortfolioManager.API", "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName
        }));
        
    options.AddOtlpExporter(otlpOptions =>
    {
        var otlpEndpoint = builder.Configuration["OTLP_ENDPOINT"] ?? "http://host.docker.internal:18889";
        otlpOptions.Endpoint = new Uri(otlpEndpoint);
    });
    options.AddConsoleExporter();
});

if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
}

// Add services to the container
builder.Services.AddControllers();

// Configure Entity Framework
builder.Services.AddDbContext<PortfolioManagerDbContext>(options =>
{
    // Use full connection string if provided (e.g. from docker-compose), 
    // otherwise build from individual config values (e.g. Key Vault + env vars)
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString))
    {
        var dbHost = builder.Configuration["DB_HOST"] ?? builder.Configuration["ConnectionStrings:Host"] ?? "localhost";
        var dbPort = builder.Configuration["DB_PORT"] ?? builder.Configuration["ConnectionStrings:Port"] ?? "5432";
        var dbName = builder.Configuration["DB_NAME"] ?? builder.Configuration["ConnectionStrings:Database"] ?? "portfolio_manager";
        var dbUsername = builder.Configuration["DB_USERNAME"] ?? builder.Configuration["ConnectionStrings:Username"] ?? "postgres";
        var dbPassword = builder.Configuration["Database:Password"] ?? builder.Configuration["DB_PASSWORD"] ?? throw new InvalidOperationException("Database password not found in configuration.");
        
        connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUsername};Password={dbPassword};Include Error Detail=false";
    }
    
    options.UseNpgsql(connectionString, npgsqlOptions => 
               npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "app"))
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, LogLevel.Information);
});

// Register application services
builder.Services.AddApplicationServices();

// Register infrastructure services  
builder.Services.AddInfrastructureServices();

// Configure EOD API options
builder.Services.Configure<EodApiOptions>(
    builder.Configuration.GetSection(EodApiOptions.SectionName));

// Configure Azure Foundry options
builder.Services.Configure<AzureFoundryOptions>(
    builder.Configuration.GetSection(AzureFoundryOptions.SectionName));

// Register API services
builder.Services.AddScoped<IPortfolioMappingService, PortfolioMappingService>();

// Add IHttpContextAccessor for user context access
builder.Services.AddHttpContextAccessor();

// Register custom metrics service
builder.Services.AddSingleton<FtoConsulting.PortfolioManager.Api.Services.MetricsService>();

// Add Authentication - using environment variables through configuration binding
// Support both Bearer (Azure AD) and SystemApiKey (for scheduled jobs) authentication
builder.Services.AddAuthentication("Bearer")
    .AddMicrosoftIdentityWebApi(options =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(options);
        
        // Add token validation parameters with extended clock skew for better session persistence
        // This helps prevent premature token rejection due to slight time differences
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5);
        options.TokenValidationParameters.ValidateLifetime = true;
    }, 
    options =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(options);
    });

// Add system API key authentication scheme for scheduled jobs
builder.Services.AddAuthentication()
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, 
        FtoConsulting.PortfolioManager.Api.Authentication.SystemApiKeyAuthenticationHandler>(
        FtoConsulting.PortfolioManager.Api.Authentication.SystemApiKeyAuthenticationHandler.SchemeName, 
        options => { });

// Add Authorization - fixed scope checking
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequirePortfolioScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        // Check for Portfolio.ReadWrite scope in various claim formats
        policy.RequireAssertion(context =>
        {
            var user = context.User;
            
            // Check all possible scope claim formats
            var allClaims = user.Claims.ToList();
            
            // Check for any claim containing Portfolio.ReadWrite
            var hasPortfolioScope = allClaims.Any(c => 
                c.Value.Contains("Portfolio.ReadWrite", StringComparison.OrdinalIgnoreCase));
            
            // Also check for the full scope URI
            var apiClientId = Environment.GetEnvironmentVariable("AZURE_AD_CLIENT_ID") ?? builder.Configuration["AzureAd:ClientId"];
            var hasFullScope = allClaims.Any(c => 
                c.Value.Contains($"api://{apiClientId}/Portfolio.ReadWrite", StringComparison.OrdinalIgnoreCase));
            
            var result = hasPortfolioScope || hasFullScope;
            
            if (!result)
            {
                var logger = context.Resource as ILogger;
                logger?.LogWarning("Authorization failed: User {User} does not have Portfolio.ReadWrite scope. Claims: {Claims}",
                    user.Identity?.Name ?? "Unknown", string.Join(", ", allClaims.Select(c => $"{c.Type}:{c.Value}")));
            }
            
            return result;
        });
    })
    .AddPolicy("SystemApiAccess", policy =>
    {
        policy.AddAuthenticationSchemes(FtoConsulting.PortfolioManager.Api.Authentication.SystemApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => 
            context.User.Identity?.AuthenticationType == 
            FtoConsulting.PortfolioManager.Api.Authentication.SystemApiKeyAuthenticationHandler.SchemeName);
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI", policy =>
    {
        // Get allowed origins from configuration with fallback to localhost
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "http://localhost:3000" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

// Configure OpenTelemetry following Microsoft Agent Framework patterns
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService("PortfolioManager.API", "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["service.instance.id"] = Environment.MachineName
    });

// Get OTLP endpoint from configuration
// Azure Container Apps injects OTEL_EXPORTER_OTLP_ENDPOINT automatically when environment telemetry is configured
// Fall back to OTLP_ENDPOINT for backward compatibility, then to local dev default
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] 
    ?? builder.Configuration["OTLP_ENDPOINT"] 
    ?? "http://host.docker.internal:18889";

// Configure tracing
var openTelemetry = builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(resourceBuilder)
            .AddSource("PortfolioManager.*")
            .AddSource("Microsoft.Extensions.AI.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
    });

// Add Azure Monitor when connection string is available (production)
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    openTelemetry.UseAzureMonitor();
}

// Configure metrics separately using Sdk.CreateMeterProviderBuilder
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter("PortfolioManager.*")
    .AddMeter("Microsoft.AspNetCore.Hosting")
    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
    .AddMeter("Microsoft.AspNetCore.Http.Connections")
    .AddMeter("Microsoft.AspNetCore.Routing")
    .AddMeter("Microsoft.AspNetCore.Diagnostics")
    .AddMeter("Microsoft.EntityFrameworkCore")
    .AddMeter("Microsoft.Extensions.AI")
    .AddMeter("Microsoft.Extensions.AI.*")
    .AddMeter("Microsoft.Agents.AI")
    .AddMeter("Microsoft.Agents.AI.*")
    .AddMeter("Azure.AI.OpenAI")
    .AddMeter("OpenAI")
    .AddMeter("System.Net.Http")
    .AddMeter("System.Net.NameResolution")
    .AddMeter("System.Runtime")
    .AddMeter("Microsoft.Extensions.Hosting")
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
    })
    .Build();

// Register the meter provider for dependency injection
builder.Services.AddSingleton(meterProvider);

// Configure logging to be exported via OpenTelemetry
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
    });
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Portfolio Manager API",
        Version = "v1",
        Description = "API for managing investment portfolios, holdings, and instruments",
        Contact = new OpenApiContact
        {
            Name = "Portfolio Manager Team",
            Email = "support@portfoliomanager.com"
        }
    });

    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Implicit = new OpenApiOAuthFlow
        {
            AuthorizationUrl = new Uri(
                $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/authorize"
            ),
            Scopes = new Dictionary<string, string>
            {
                {
                    $"api://{builder.Configuration["AzureAd:ClientId"]}/Portfolio.ReadWrite",
                    "Read and write portfolio data"
                }
            }
        }
        }
    });

    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("oauth2"),
            new List<string> { $"api://{builder.Configuration["AzureAd:ClientId"]}/Portfolio.ReadWrite" }
        }
    });
    // Include XML comments for better documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }


});

var app = builder.Build();

// Log CORS configuration on startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
logger.LogInformation("CORS configured with allowed origins: {Origins}", allowedOrigins != null ? string.Join(", ", allowedOrigins) : "null (using fallback)");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
app.UseSwagger();
app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Portfolio Manager API v1");
        c.RoutePrefix = ""; 
        c.DocumentTitle = "Portfolio Manager API";
        c.DefaultModelsExpandDepth(-1);
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);

        // OAuth2 for Swagger UI (Implicit flow)
        c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]); // uses environment variable
        c.OAuthAppName("Portfolio Manager API");
        c.OAuthScopeSeparator(" ");
        // IMPORTANT: do NOT call c.OAuthUsePkce() when using Implicit flow
    });
}

// Enable CORS - must be before UseHttpsRedirection
app.UseCors("AllowUI");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map health checks endpoint
// Add health checks endpoint with logging
app.MapGet("/health", (ILogger<Program> logger) => 
{
    using (logger.BeginScope("Health Check Request"))
    {
        logger.LogInformation("Health check endpoint called at {Timestamp} from environment {Environment} with telemetry {TelemetryEnabled}", 
            DateTime.UtcNow, 
            app.Environment.EnvironmentName, 
            "OpenTelemetry+Aspire Dashboard");
    }
    
    return Results.Ok(new { 
        Status = "Healthy", 
        Timestamp = DateTime.UtcNow,
        Environment = app.Environment.EnvironmentName,
        Telemetry = "OpenTelemetry with Aspire Dashboard and Structured Logging"
    });
});

app.MapHealthChecks("/health/detailed");

// Add debug endpoint to check token claims (temporary - no auth required)
app.MapGet("/debug/claims", (HttpContext context) => 
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        return Results.Ok(new { 
            IsAuthenticated = false,
            Message = "No authentication token present",
            Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
        });
    }
    
    var claims = context.User.Claims.Select(c => new { 
        Type = c.Type, 
        Value = c.Value 
    }).ToList();
    
    return Results.Ok(new { 
        IsAuthenticated = context.User.Identity.IsAuthenticated,
        Claims = claims
    });
});

// Add a simple authenticated endpoint for testing
app.MapGet("/debug/auth-test", (HttpContext context) => 
{
    return Results.Ok(new { 
        Message = "You are authenticated!",
        IsAuthenticated = context.User.Identity?.IsAuthenticated ?? false,
        UserName = context.User.Identity?.Name ?? "Unknown",
        ClaimsCount = context.User.Claims.Count(),
        AllClaims = context.User.Claims.Select(c => new { c.Type, c.Value }).ToList()
    });
}).RequireAuthorization("RequirePortfolioScope");

app.MapControllers();

app.Run();
