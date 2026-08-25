using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    public partial class ApplicationReviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManualId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    UnverifiedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "application_review_criteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    Findings = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_review_criteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_review_criteria_application_reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "application_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_review_criteria_ReviewId",
                table: "application_review_criteria",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_application_reviews_ChatId_CreatedAt",
                table: "application_reviews",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_application_reviews_OwnerId_CreatedAt",
                table: "application_reviews",
                columns: new[] { "OwnerId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_review_criteria");

            migrationBuilder.DropTable(
                name: "application_reviews");
        }
    }
}
