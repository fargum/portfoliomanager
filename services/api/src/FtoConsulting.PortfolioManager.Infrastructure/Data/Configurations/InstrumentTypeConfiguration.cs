using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class InstrumentTypeConfiguration : IEntityTypeConfiguration<InstrumentType>
{
    public void Configure(EntityTypeBuilder<InstrumentType> builder)
    {
        builder.ToTable("instrument_types", "app");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Index on Name for quick lookups
        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("ix_instrument_types_name");

        // Configure relationships
        builder.HasMany(x => x.Instruments)
            .WithOne(x => x.InstrumentType)
            .HasForeignKey(x => x.InstrumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}