using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Infrastructure.Persistence.Migrations
{
    /// <summary>Adds the customer-visible shipment snapshot to Ordering.</summary>
    public partial class AddShipmentSnapshot : Migration
    {
        /// <summary>Applies the service-owned schema additions.</summary>
        /// <param name="migrationBuilder">Migration operation builder.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentId",
                schema: "ordering",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                schema: "ordering",
                table: "orders",
                type: "text",
                nullable: true);
        }

        /// <summary>Removes the schema additions when rolling back this migration.</summary>
        /// <param name="migrationBuilder">Migration operation builder.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShipmentId",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                schema: "ordering",
                table: "orders");
        }
    }
}
