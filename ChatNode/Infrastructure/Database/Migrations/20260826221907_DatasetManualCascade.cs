using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DatasetManualCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM datasets WHERE \"ManualId\" NOT IN (SELECT \"Id\" FROM manuals)");

            migrationBuilder.AddForeignKey(
                name: "FK_datasets_manuals_ManualId",
                table: "datasets",
                column: "ManualId",
                principalTable: "manuals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_datasets_manuals_ManualId",
                table: "datasets");
        }
    }
}
