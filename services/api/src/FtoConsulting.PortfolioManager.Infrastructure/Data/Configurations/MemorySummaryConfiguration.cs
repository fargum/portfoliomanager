using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MemorySummary entity
/// </summary>
public class MemorySummaryConfiguration : IEntityTypeConfiguration<MemorySummary>
{
    public void Configure(EntityTypeBuilder<MemorySummary> builder)
    {
        builder.ToTable("memory_summaries", "app");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ConversationThreadId)
            .HasColumnName("conversation_thread_id")
            .IsRequired();

        builder.Property(x => x.SummaryDate)
            .HasColumnName("summary_date")
            .IsRequired();

        builder.Property(x => x.Summary)
            .HasColumnName("summary")
            .IsRequired();

        builder.Property(x => x.KeyTopics)
            .HasColumnName("key_topics")
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.UserPreferences)
            .HasColumnName("user_preferences")
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.MessageCount)
            .HasColumnName("message_count")
            .IsRequired();

        builder.Property(x => x.TotalTokens)
            .HasColumnName("total_tokens")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes for performance
        builder.HasIndex(x => x.ConversationThreadId)
            .HasDatabaseName("ix_memory_summaries_conversation_thread_id");

        builder.HasIndex(x => x.SummaryDate)
            .HasDatabaseName("ix_memory_summaries_summary_date");

        builder.HasIndex(x => new { x.ConversationThreadId, x.SummaryDate })
            .IsUnique()
            .HasDatabaseName("ix_memory_summaries_thread_id_date");

        // Configure relationships
        builder.HasOne(x => x.ConversationThread)
            .WithMany(x => x.Summaries)
            .HasForeignKey(x => x.ConversationThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}