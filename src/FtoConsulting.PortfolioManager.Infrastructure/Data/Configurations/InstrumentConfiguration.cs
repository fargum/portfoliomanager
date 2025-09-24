using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("Instruments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.ISIN)
            .IsRequired()
            .HasMaxLength(12); // ISIN is always 12 characters

        builder.Property(x => x.SEDOL)
            .HasMaxLength(7); // SEDOL is 7 characters

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.InstrumentTypeId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        // Indexes for quick lookups
        builder.HasIndex(x => x.ISIN)
            .IsUnique()
            .HasDatabaseName("IX_Instruments_ISIN");

        builder.HasIndex(x => x.SEDOL)
            .HasDatabaseName("IX_Instruments_SEDOL");

        builder.HasIndex(x => x.Name)
            .HasDatabaseName("IX_Instruments_Name");

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