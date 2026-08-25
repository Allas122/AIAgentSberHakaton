using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    public partial class TokenUsage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "token_usage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    ToolCalls = table.Column<int>(type: "integer", nullable: false),
                    Partial = table.Column<bool>(type: "boolean", nullable: false),
                    Failed = table.Column<bool>(type: "boolean", nullable: false),
                    BalanceSpent = table.Column<double>(type: "double precision", nullable: true),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token_usage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_token_usage_Operation_StartedAt",
                table: "token_usage",
                columns: new[] { "Operation", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_token_usage_StartedAt",
                table: "token_usage",
                column: "StartedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "token_usage");
        }
    }
}
