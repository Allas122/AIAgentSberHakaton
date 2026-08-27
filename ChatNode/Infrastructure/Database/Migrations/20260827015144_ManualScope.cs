using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ManualScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "manuals",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Public");

            migrationBuilder.CreateIndex(
                name: "IX_manuals_Scope",
                table: "manuals",
                column: "Scope");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_manuals_Scope",
                table: "manuals");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "manuals");
        }
    }
}
