using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("instruments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.ISIN)
            .HasColumnName("isin")
            .IsRequired()
            .HasMaxLength(12); // ISIN is always 12 characters

        builder.Property(x => x.SEDOL)
            .HasColumnName("sedol")
            .HasMaxLength(7); // SEDOL is 7 characters

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(x => x.Ticker)
            .HasColumnName("ticker")
            .HasMaxLength(50);

        builder.Property(x => x.InstrumentTypeId)
            .HasColumnName("instrument_type_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes for quick lookups
        builder.HasIndex(x => x.ISIN)
            .IsUnique()
            .HasDatabaseName("ix_instruments_isin");

        builder.HasIndex(x => x.SEDOL)
            .HasDatabaseName("ix_instruments_sedol");

        builder.HasIndex(x => x.Name)
            .HasDatabaseName("ix_instruments_name");

        builder.HasIndex(x => x.Ticker)
            .HasDatabaseName("ix_instruments_ticker");

        // Configure relationships
        builder.HasOne(x => x.InstrumentType)
            .WithMany(x => x.Instruments)
            .HasForeignKey(x => x.InstrumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Holdings)
            .WithOne(x => x.Instrument)
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}