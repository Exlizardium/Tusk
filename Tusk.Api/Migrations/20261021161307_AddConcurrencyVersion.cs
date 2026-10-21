using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tusk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Tickets",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Projects");
        }
    }
}
