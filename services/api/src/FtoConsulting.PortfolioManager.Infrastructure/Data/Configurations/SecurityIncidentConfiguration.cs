using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for SecurityIncident entity
/// </summary>
public class SecurityIncidentConfiguration : IEntityTypeConfiguration<SecurityIncident>
{
    public void Configure(EntityTypeBuilder<SecurityIncident> builder)
    {
        builder.ToTable("security_incidents", "app");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(x => x.ViolationType)
            .HasColumnName("violation_type")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Context)
            .HasColumnName("context")
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.ThreatLevel)
            .HasColumnName("threat_level")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.AdditionalData)
            .HasColumnName("additional_data")
            .HasColumnType("jsonb");

        builder.Property(x => x.IsResolved)
            .HasColumnName("is_resolved")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(x => x.Resolution)
            .HasColumnName("resolution")
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes for performance and security monitoring
        builder.HasIndex(x => x.AccountId)
            .HasDatabaseName("ix_security_incidents_account_id");

        builder.HasIndex(x => x.ViolationType)
            .HasDatabaseName("ix_security_incidents_violation_type");

        builder.HasIndex(x => x.Severity)
            .HasDatabaseName("ix_security_incidents_severity");

        builder.HasIndex(x => x.ThreatLevel)
            .HasDatabaseName("ix_security_incidents_threat_level");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("ix_security_incidents_created_at");

        builder.HasIndex(x => x.IsResolved)
            .HasDatabaseName("ix_security_incidents_is_resolved");

        builder.HasIndex(x => new { x.AccountId, x.CreatedAt })
            .HasDatabaseName("ix_security_incidents_account_id_created_at");

        builder.HasIndex(x => new { x.ThreatLevel, x.IsResolved })
            .HasDatabaseName("ix_security_incidents_threat_level_resolved");

        // Configure relationships
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}