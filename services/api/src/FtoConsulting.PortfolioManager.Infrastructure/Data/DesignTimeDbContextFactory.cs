using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PortfolioManagerDbContext>
{
    public PortfolioManagerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../FtoConsulting.PortfolioManager.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets("891f0d69-b776-435e-a2c6-650369ce9179") // Using the UserSecretsId directly
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<PortfolioManagerDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        optionsBuilder.UseNpgsql(connectionString);

        return new PortfolioManagerDbContext(optionsBuilder.Options);
    }
}