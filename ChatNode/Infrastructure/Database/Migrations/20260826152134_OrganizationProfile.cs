using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_profiles",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Fax = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Website = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Okpo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Ogrn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Inn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Kpp = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SignerPosition = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SignerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContactPosition = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_profiles", x => x.OwnerId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_profiles");
        }
    }
}
