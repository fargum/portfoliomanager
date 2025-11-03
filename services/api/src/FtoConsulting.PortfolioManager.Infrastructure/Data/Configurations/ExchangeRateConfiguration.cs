using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for ExchangeRate
/// </summary>
public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        // Table name and schema
        builder.ToTable("exchange_rates", "app");

        // Primary key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.BaseCurrency)
            .HasColumnName("base_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.TargetCurrency)
            .HasColumnName("target_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.Rate)
            .HasColumnName("rate")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(e => e.RateDate)
            .HasColumnName("rate_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.Source)
            .HasColumnName("source")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.FetchedAt)
            .HasColumnName("fetched_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Base entity properties (inherited)
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // Indexes
        builder.HasIndex(e => new { e.BaseCurrency, e.TargetCurrency, e.RateDate })
            .HasDatabaseName("ix_exchange_rates_currency_pair_date")
            .IsUnique();

        builder.HasIndex(e => e.RateDate)
            .HasDatabaseName("ix_exchange_rates_rate_date");

        builder.HasIndex(e => new { e.BaseCurrency, e.TargetCurrency })
            .HasDatabaseName("ix_exchange_rates_currency_pair");
    }
}