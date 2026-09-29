using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationCorrelationAndExpiryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                schema: "inventory",
                table: "stock_reservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing reservations predate correlation persistence; OrderId is a stable trace fallback.
            migrationBuilder.Sql("""
                UPDATE inventory.stock_reservations
                SET "CorrelationId" = "OrderId"
                WHERE "CorrelationId" = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_Status_ExpiresAtUtc",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_reservations_Status_ExpiresAtUtc",
                schema: "inventory",
                table: "stock_reservations");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "inventory",
                table: "stock_reservations");
        }
    }
}
