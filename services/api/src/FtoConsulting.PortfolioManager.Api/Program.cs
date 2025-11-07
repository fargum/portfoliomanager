using FtoConsulting.PortfolioManager.Api.Extensions;
using FtoConsulting.PortfolioManager.Api.Services;
using FtoConsulting.PortfolioManager.Application;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Infrastructure.Data;
using FtoConsulting.PortfolioManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Globalization;
using System.Reflection;

// Configure culture for consistent date parsing
var cultureInfo = new CultureInfo("en-GB"); // UK culture for dd/MM/yyyy preference
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

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

// Register AI services
builder.Services.AddAiServices();

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
app.MapHealthChecks("/health");

app.MapControllers();

// Initialize AI services
await app.InitializeAiServicesAsync();

app.Run();
