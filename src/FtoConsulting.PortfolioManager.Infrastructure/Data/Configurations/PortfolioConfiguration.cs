using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AccountId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        // Index for quick lookups
        builder.HasIndex(x => new { x.AccountId, x.Name })
            .IsUnique()
            .HasDatabaseName("IX_Portfolios_AccountId_Name");

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