using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tusk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CloseReason",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedById",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ClosedById",
                table: "Tickets",
                column: "ClosedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_ClosedById",
                table: "Tickets",
                column: "ClosedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                UPDATE "Tickets" SET "ClosedAt" = COALESCE("UpdatedAt", "CreatedAt") WHERE "Status" IN (2, 3);
                UPDATE "Tickets" SET "Status" = 3, "CloseReason" = 0 WHERE "Status" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Tickets" SET "Status" = 2 WHERE "Status" = 3 AND "CloseReason" = 0;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_ClosedById",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ClosedById",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CloseReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ClosedById",
                table: "Tickets");
        }
    }
}
