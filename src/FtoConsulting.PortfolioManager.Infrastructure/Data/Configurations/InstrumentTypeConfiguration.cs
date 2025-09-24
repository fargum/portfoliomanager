using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class InstrumentTypeConfiguration : IEntityTypeConfiguration<InstrumentType>
{
    public void Configure(EntityTypeBuilder<InstrumentType> builder)
    {
        builder.ToTable("InstrumentTypes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        // Index on Name for quick lookups
        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("IX_InstrumentTypes_Name");

        // Configure relationships
        builder.HasMany(x => x.Instruments)
            .WithOne(x => x.InstrumentType)
            .HasForeignKey(x => x.InstrumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}