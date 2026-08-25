using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    public partial class InitialAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Assignee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_CreatedAt",
                table: "assignments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_OwnerId_CreatedAt",
                table: "assignments",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_Status",
                table: "assignments",
                column: "Status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments");
        }
    }
}
