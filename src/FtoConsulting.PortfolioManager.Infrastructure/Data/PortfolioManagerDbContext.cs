using Microsoft.EntityFrameworkCore;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data;

public class PortfolioManagerDbContext : DbContext
{
    public PortfolioManagerDbContext(DbContextOptions<PortfolioManagerDbContext> options)
        : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Portfolio> Portfolios { get; set; }
    public DbSet<Holding> Holdings { get; set; }
    public DbSet<Instrument> Instruments { get; set; }
    public DbSet<InstrumentType> InstrumentTypes { get; set; }
    public DbSet<Platform> Platforms { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortfolioManagerDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // This will be overridden at runtime, but provides fallback for design-time
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=portfolio_manager;Username=migrator;Password=!Gangatala10!")
                         .UseSnakeCaseNamingConvention();
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps before saving
        var entries = ChangeTracker.Entries<BaseEntity>();
        
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
            {
                var updateMethod = entry.Entity.GetType().GetMethod("SetUpdatedAt", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateMethod?.Invoke(entry.Entity, null);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}