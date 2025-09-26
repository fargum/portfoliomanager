using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("holdings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.ValuationDate)
            .HasColumnName("valuation_date")
            .IsRequired();

        builder.Property(x => x.InstrumentId)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(x => x.PlatformId)
            .HasColumnName("platform_id")
            .IsRequired();

        builder.Property(x => x.PortfolioId)
            .HasColumnName("portfolio_id")
            .IsRequired();

        builder.Property(x => x.UnitAmount)
            .HasColumnName("unit_amount")
            .IsRequired()
            .HasPrecision(18, 8); // High precision for unit amounts

        builder.Property(x => x.BoughtValue)
            .HasColumnName("bought_value")
            .IsRequired()
            .HasPrecision(18, 2); // Standard currency precision

        builder.Property(x => x.CurrentValue)
            .HasColumnName("current_value")
            .IsRequired()
            .HasPrecision(18, 2); // Standard currency precision

        builder.Property(x => x.DailyProfitLoss)
            .HasColumnName("daily_profit_loss")
            .HasPrecision(18, 2);

        builder.Property(x => x.DailyProfitLossPercentage)
            .HasColumnName("daily_profit_loss_percentage")
            .HasPrecision(18, 4); // Higher precision for percentages

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes for efficient queries
        builder.HasIndex(x => x.PortfolioId)
            .HasDatabaseName("ix_holdings_portfolio_id");

        builder.HasIndex(x => x.ValuationDate)
            .HasDatabaseName("ix_holdings_valuation_date");

        builder.HasIndex(x => new { x.PortfolioId, x.InstrumentId, x.ValuationDate })
            .HasDatabaseName("ix_holdings_portfolio_instrument_date");

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