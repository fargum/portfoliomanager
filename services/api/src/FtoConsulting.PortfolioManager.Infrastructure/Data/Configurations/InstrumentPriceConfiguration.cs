using FtoConsulting.PortfolioManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for InstrumentPrice entity
/// </summary>
public class InstrumentPriceConfiguration : IEntityTypeConfiguration<InstrumentPrice>
{
    public void Configure(EntityTypeBuilder<InstrumentPrice> builder)
    {
        // Table name using snake_case convention
        builder.ToTable("instrument_prices", "app");

        // Composite primary key
        builder.HasKey(ip => new { ip.InstrumentId, ip.ValuationDate });

        // Configure properties with snake_case column names
        builder.Property(ip => ip.InstrumentId)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(ip => ip.ValuationDate)
            .HasColumnName("valuation_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(ip => ip.Ticker)
            .HasColumnName("ticker")
            .HasMaxLength(50);

        builder.Property(ip => ip.Name)
            .HasColumnName("name")
            .HasMaxLength(200);

        builder.Property(ip => ip.Price)
            .HasColumnName("price")
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(ip => ip.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(ip => ip.Change)
            .HasColumnName("change")
            .HasColumnType("decimal(18,4)");

        builder.Property(ip => ip.ChangePercent)
            .HasColumnName("change_percent")
            .HasColumnType("decimal(8,4)");

        builder.Property(ip => ip.PreviousClose)
            .HasColumnName("previous_close")
            .HasColumnType("decimal(18,4)");

        builder.Property(ip => ip.Open)
            .HasColumnName("open")
            .HasColumnType("decimal(18,4)");

        builder.Property(ip => ip.High)
            .HasColumnName("high")
            .HasColumnType("decimal(18,4)");

        builder.Property(ip => ip.Low)
            .HasColumnName("low")
            .HasColumnType("decimal(18,4)");

        builder.Property(ip => ip.Volume)
            .HasColumnName("volume");

        builder.Property(ip => ip.Market)
            .HasColumnName("market")
            .HasMaxLength(50);

        builder.Property(ip => ip.MarketStatus)
            .HasColumnName("market_status")
            .HasMaxLength(20);

        builder.Property(ip => ip.PriceTimestamp)
            .HasColumnName("price_timestamp")
            .HasColumnType("timestamp with time zone")
            .HasConversion(
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
                v => v);

        // Audit properties with UTC conversion
        builder.Property(ip => ip.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasConversion(
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                v => v)
            .IsRequired();

        builder.Property(ip => ip.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasConversion(
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
                v => v);

        // Foreign key relationship to Instrument
        builder.HasOne(ip => ip.Instrument)
            .WithMany()
            .HasForeignKey(ip => ip.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(ip => ip.ValuationDate)
            .HasDatabaseName("ix_instrument_prices_valuation_date");

        builder.HasIndex(ip => ip.InstrumentId)
            .HasDatabaseName("ix_instrument_prices_instrument_id");

        builder.HasIndex(ip => ip.Ticker)
            .HasDatabaseName("ix_instrument_prices_ticker");
    }
}