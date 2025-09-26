using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("portfolios");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Index for quick lookups
        builder.HasIndex(x => new { x.AccountId, x.Name })
            .IsUnique()
            .HasDatabaseName("ix_portfolios_account_id_name");

        // Configure relationships
        builder.HasOne(x => x.Account)
            .WithMany(x => x.Portfolios)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Holdings)
            .WithOne(x => x.Portfolio)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore calculated properties (they are computed, not stored)
        builder.Ignore(x => x.TotalValue);
        builder.Ignore(x => x.TotalBoughtValue);
        builder.Ignore(x => x.TotalProfitLoss);
        builder.Ignore(x => x.TotalDailyProfitLoss);
        builder.Ignore(x => x.TotalProfitLossPercentage);
        builder.Ignore(x => x.TotalDailyProfitLossPercentage);

        // Ignore domain events collection from AggregateRoot
        builder.Ignore(x => x.DomainEvents);
    }
}