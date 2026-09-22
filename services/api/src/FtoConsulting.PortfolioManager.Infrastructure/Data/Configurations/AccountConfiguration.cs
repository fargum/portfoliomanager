using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", "app", table => table.HasCheckConstraint(
            "ck_accounts_identity_mode",
            "(mode = 0 AND owner_account_id IS NULL AND external_user_id IS NOT NULL AND email IS NOT NULL) OR " +
            "(mode = 1 AND owner_account_id IS NOT NULL AND owner_account_id <> id AND external_user_id IS NULL AND email IS NULL)"));

        builder.Property(x => x.Mode).HasColumnName("mode").HasConversion<int>().HasDefaultValue(AccountMode.Personal);
        builder.Property(x => x.OwnerAccountId).HasColumnName("owner_account_id");
        builder.HasIndex(x => x.OwnerAccountId).IsUnique().HasDatabaseName("ix_accounts_owner_account_id");
        builder.HasOne(x => x.OwnerAccount).WithOne().HasForeignKey<Account>(x => x.OwnerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalUserId)
            .HasColumnName("external_user_id")
            .IsRequired(false)
            .HasMaxLength(255);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .IsRequired(false)
            .HasMaxLength(255);

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes for quick lookups
        builder.HasIndex(x => x.ExternalUserId)
            .IsUnique()
            .HasDatabaseName("ix_accounts_external_user_id");

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("ix_accounts_email");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("ix_accounts_is_active");

        // Configure relationships
        builder.HasMany(x => x.Portfolios)
            .WithOne(x => x.Account)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
