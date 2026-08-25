using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    public partial class ManualProcessingStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedChunks",
                table: "manuals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedChunks",
                table: "manuals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "manuals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Ready");

            migrationBuilder.AddColumn<string>(
                name: "StatusDetail",
                table: "manuals",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalChunks",
                table: "manuals",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedChunks",
                table: "manuals");

            migrationBuilder.DropColumn(
                name: "ProcessedChunks",
                table: "manuals");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "manuals");

            migrationBuilder.DropColumn(
                name: "StatusDetail",
                table: "manuals");

            migrationBuilder.DropColumn(
                name: "TotalChunks",
                table: "manuals");
        }
    }
}
