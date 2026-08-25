using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    public partial class ReviewDocumentLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "application_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageId",
                table: "application_reviews",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_reviews_DocumentId",
                table: "application_reviews",
                column: "DocumentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_application_reviews_DocumentId",
                table: "application_reviews");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "application_reviews");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "application_reviews");
        }
    }
}
