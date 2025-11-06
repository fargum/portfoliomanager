using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversation_threads",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    thread_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    last_activity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_threads", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversation_threads_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "app",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_thread_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    message_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_messages_conversation_threads_conversation_thread_id",
                        column: x => x.conversation_thread_id,
                        principalSchema: "app",
                        principalTable: "conversation_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memory_summaries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_thread_id = table.Column<int>(type: "integer", nullable: false),
                    summary_date = table.Column<DateOnly>(type: "date", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    key_topics = table.Column<string>(type: "jsonb", nullable: false),
                    user_preferences = table.Column<string>(type: "jsonb", nullable: false),
                    message_count = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_summaries_conversation_threads_conversation_thread_id",
                        column: x => x.conversation_thread_id,
                        principalSchema: "app",
                        principalTable: "conversation_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_conversation_thread_id",
                schema: "app",
                table: "chat_messages",
                column: "conversation_thread_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_message_timestamp",
                schema: "app",
                table: "chat_messages",
                column: "message_timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_thread_id_timestamp",
                schema: "app",
                table: "chat_messages",
                columns: new[] { "conversation_thread_id", "message_timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_threads_account_id",
                schema: "app",
                table: "conversation_threads",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_threads_account_id_is_active",
                schema: "app",
                table: "conversation_threads",
                columns: new[] { "account_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_threads_last_activity",
                schema: "app",
                table: "conversation_threads",
                column: "last_activity");

            migrationBuilder.CreateIndex(
                name: "ix_memory_summaries_conversation_thread_id",
                schema: "app",
                table: "memory_summaries",
                column: "conversation_thread_id");

            migrationBuilder.CreateIndex(
                name: "ix_memory_summaries_summary_date",
                schema: "app",
                table: "memory_summaries",
                column: "summary_date");

            migrationBuilder.CreateIndex(
                name: "ix_memory_summaries_thread_id_date",
                schema: "app",
                table: "memory_summaries",
                columns: new[] { "conversation_thread_id", "summary_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "memory_summaries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "conversation_threads",
                schema: "app");
        }
    }
}
