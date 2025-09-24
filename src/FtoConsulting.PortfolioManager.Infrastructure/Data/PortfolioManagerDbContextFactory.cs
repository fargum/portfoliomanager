using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data;

public class PortfolioManagerDbContextFactory : IDesignTimeDbContextFactory<PortfolioManagerDbContext>
{
    public PortfolioManagerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PortfolioManagerDbContext>();
        
        // Use a default connection string for design-time operations (migrations)
        // This will be replaced with actual connection string at runtime
        optionsBuilder.UseNpgsql("Host=localhost;Database=PortfolioManager_Design;Username=postgres;Password=password");

        return new PortfolioManagerDbContext(optionsBuilder.Options);
    }
}