using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for ChatMessage entity
/// </summary>
public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages", "app");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ConversationThreadId)
            .HasColumnName("conversation_thread_id")
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(x => x.TokenCount)
            .HasColumnName("token_count")
            .IsRequired();

        builder.Property(x => x.MessageTimestamp)
            .HasColumnName("message_timestamp")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Indexes for performance
        builder.HasIndex(x => x.ConversationThreadId)
            .HasDatabaseName("ix_chat_messages_conversation_thread_id");

        builder.HasIndex(x => x.MessageTimestamp)
            .HasDatabaseName("ix_chat_messages_message_timestamp");

        builder.HasIndex(x => new { x.ConversationThreadId, x.MessageTimestamp })
            .HasDatabaseName("ix_chat_messages_thread_id_timestamp");

        // Configure relationships
        builder.HasOne(x => x.ConversationThread)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}