using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class LetterFormsAndExecutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutorName",
                table: "organization_profiles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExecutorPhone",
                table: "organization_profiles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FormFileName",
                table: "letter_templates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormPlaceholders",
                table: "letter_templates",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FormSizeBytes",
                table: "letter_templates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormStorageKey",
                table: "letter_templates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutorName",
                table: "organization_profiles");

            migrationBuilder.DropColumn(
                name: "ExecutorPhone",
                table: "organization_profiles");

            migrationBuilder.DropColumn(
                name: "FormFileName",
                table: "letter_templates");

            migrationBuilder.DropColumn(
                name: "FormPlaceholders",
                table: "letter_templates");

            migrationBuilder.DropColumn(
                name: "FormSizeBytes",
                table: "letter_templates");

            migrationBuilder.DropColumn(
                name: "FormStorageKey",
                table: "letter_templates");
        }
    }
}
