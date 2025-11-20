using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Globalization;
using System.Reflection;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;

// Configure culture for consistent date parsing
var cultureInfo = new CultureInfo("en-GB"); // UK culture for dd/MM/yyyy preference
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

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
        otlpOptions.Endpoint = new Uri("http://host.docker.internal:18889");
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
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
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

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Allow the UI container
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

// Configure OpenTelemetry tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("PortfolioManager.API", "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName,
                    ["service.instance.id"] = Environment.MachineName
                }))
            .AddSource("PortfolioManager.*")
            .AddSource("Microsoft.Extensions.AI.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://host.docker.internal:18889");
            })
            .AddConsoleExporter();
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

    // Include XML comments for better documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }


});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Portfolio Manager API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger at the root
        c.DocumentTitle = "Portfolio Manager API";
        c.DefaultModelsExpandDepth(-1);
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

// Enable CORS
app.UseCors("AllowUI");

app.UseHttpsRedirection();
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

app.MapControllers();

app.Run();
