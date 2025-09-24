using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("Holdings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.ValuationDate)
            .IsRequired();

        builder.Property(x => x.InstrumentId)
            .IsRequired();

        builder.Property(x => x.PlatformId)
            .IsRequired();

        builder.Property(x => x.PortfolioId)
            .IsRequired();

        builder.Property(x => x.UnitAmount)
            .IsRequired()
            .HasPrecision(18, 8); // High precision for unit amounts

        builder.Property(x => x.BoughtValue)
            .IsRequired()
            .HasPrecision(18, 2); // Standard currency precision

        builder.Property(x => x.CurrentValue)
            .IsRequired()
            .HasPrecision(18, 2); // Standard currency precision

        builder.Property(x => x.DailyProfitLoss)
            .HasPrecision(18, 2);

        builder.Property(x => x.DailyProfitLossPercentage)
            .HasPrecision(18, 4); // Higher precision for percentages

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        // Indexes for efficient queries
        builder.HasIndex(x => x.PortfolioId)
            .HasDatabaseName("IX_Holdings_PortfolioId");

        builder.HasIndex(x => x.ValuationDate)
            .HasDatabaseName("IX_Holdings_ValuationDate");

        builder.HasIndex(x => new { x.PortfolioId, x.InstrumentId, x.ValuationDate })
            .HasDatabaseName("IX_Holdings_Portfolio_Instrument_Date");

        // Configure relationships
        builder.HasOne(x => x.Portfolio)
            .WithMany(x => x.Holdings)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Instrument)
            .WithMany(x => x.Holdings)
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Platform)
            .WithMany(x => x.Holdings)
            .HasForeignKey(x => x.PlatformId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore calculated properties (they are computed, not stored)
        builder.Ignore(x => x.TotalProfitLoss);
        builder.Ignore(x => x.TotalProfitLossPercentage);
    }
}