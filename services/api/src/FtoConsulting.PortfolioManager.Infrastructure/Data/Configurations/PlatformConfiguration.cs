using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    public void Configure(EntityTypeBuilder<Platform> builder)
    {
        builder.ToTable("platforms", "app");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Index on Name for quick lookups
        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("ix_platforms_name");

        // Configure relationships
        builder.HasMany(x => x.Holdings)
            .WithOne(x => x.Platform)
            .HasForeignKey(x => x.PlatformId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}