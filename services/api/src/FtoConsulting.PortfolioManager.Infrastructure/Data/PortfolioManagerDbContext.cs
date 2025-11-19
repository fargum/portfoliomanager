using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
    public DbSet<InstrumentPrice> InstrumentPrices { get; set; }
    public DbSet<ExchangeRate> ExchangeRates { get; set; }
    
    // Memory entities for AI conversation persistence
    public DbSet<ConversationThread> ConversationThreads { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<MemorySummary> MemorySummaries { get; set; }
    
    // Security entities for AI guardrail monitoring
    public DbSet<SecurityIncident> SecurityIncidents { get; set; }

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
            // Use environment variable or safer fallback for design-time operations
            var connectionString = Environment.GetEnvironmentVariable("PORTFOLIO_DB_CONNECTION") 
                ?? "Host=localhost;Port=5432;Database=portfolio_manager;Username=postgres;Password=design_time_placeholder";
            optionsBuilder.UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "app"));
                        // .UseSnakeCaseNamingConvention();
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps before saving
        var entries = ChangeTracker.Entries<BaseEntity>();
        
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                // Ensure CreatedAt is set to UTC for new entities
                var createdAtProperty = entry.Property(nameof(BaseEntity.CreatedAt));
                if (createdAtProperty.CurrentValue is DateTime createdAt && createdAt.Kind != DateTimeKind.Utc)
                {
                    createdAtProperty.CurrentValue = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var updateMethod = entry.Entity.GetType().GetMethod("SetUpdatedAt", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateMethod?.Invoke(entry.Entity, null);
                
                // Ensure UpdatedAt is set to UTC for modified entities
                var updatedAtProperty = entry.Property(nameof(BaseEntity.UpdatedAt));
                if (updatedAtProperty.CurrentValue is DateTime updatedAt && updatedAt.Kind != DateTimeKind.Utc)
                {
                    updatedAtProperty.CurrentValue = DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc);
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}