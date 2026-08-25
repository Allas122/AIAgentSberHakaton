using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    public partial class AssignmentCurator : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeId",
                table: "assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_assignments_AssigneeId_CreatedAt",
                table: "assignments",
                columns: new[] { "AssigneeId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_assignments_AssigneeId_CreatedAt",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "assignments");
        }
    }
}
