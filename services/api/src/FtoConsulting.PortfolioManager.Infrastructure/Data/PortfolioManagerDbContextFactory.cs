using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data;

public class PortfolioManagerDbContextFactory : IDesignTimeDbContextFactory<PortfolioManagerDbContext>
{
    public PortfolioManagerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PortfolioManagerDbContext>();
        
        // Try to get connection string from environment variable first, then use default
        var connectionString = Environment.GetEnvironmentVariable("PORTFOLIO_DB_CONNECTION") 
            ?? "Host=localhost;Port=5432;Database=portfolio_manager;Username=your_username;Password=your_password";
        
        optionsBuilder.UseNpgsql(connectionString);

        return new PortfolioManagerDbContext(optionsBuilder.Options);
    }
}