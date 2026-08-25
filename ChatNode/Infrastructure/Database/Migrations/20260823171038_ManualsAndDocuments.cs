using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    public partial class ManualsAndDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manuals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Navigation = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manuals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stored_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manual_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManualId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Navigation = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manual_parts_manuals_ManualId",
                        column: x => x.ManualId,
                        principalTable: "manuals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manual_parts_ManualId",
                table: "manual_parts",
                column: "ManualId");

            migrationBuilder.CreateIndex(
                name: "IX_stored_documents_ChatId",
                table: "stored_documents",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_stored_documents_OwnerId_CreatedAt",
                table: "stored_documents",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stored_documents_StorageKey",
                table: "stored_documents",
                column: "StorageKey",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manual_parts");

            migrationBuilder.DropTable(
                name: "stored_documents");

            migrationBuilder.DropTable(
                name: "manuals");
        }
    }
}
