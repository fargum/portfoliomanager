using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for ConversationThread entity
/// </summary>
public class ConversationThreadConfiguration : IEntityTypeConfiguration<ConversationThread>
{
    public void Configure(EntityTypeBuilder<ConversationThread> builder)
    {
        builder.ToTable("conversation_threads", "app");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(x => x.ThreadTitle)
            .HasColumnName("thread_title")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.LastActivity)
            .HasColumnName("last_activity")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes for performance
        builder.HasIndex(x => x.AccountId)
            .HasDatabaseName("ix_conversation_threads_account_id");

        builder.HasIndex(x => new { x.AccountId, x.IsActive })
            .HasDatabaseName("ix_conversation_threads_account_id_is_active");

        builder.HasIndex(x => x.LastActivity)
            .HasDatabaseName("ix_conversation_threads_last_activity");

        // Configure relationships
        builder.HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.ConversationThread)
            .HasForeignKey(x => x.ConversationThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Summaries)
            .WithOne(x => x.ConversationThread)
            .HasForeignKey(x => x.ConversationThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}