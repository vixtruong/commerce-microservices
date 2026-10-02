using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Persistence.Migrations
{
    /// <summary>Adds Inventory-owned physical-stock adjustment audit records.</summary>
    public partial class AddStockAdjustmentAudit : Migration
    {
        /// <summary>Applies the service-owned schema additions.</summary>
        /// <param name="migrationBuilder">Migration operation builder.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_adjustments",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Delta = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_adjustments_ProductId_CreatedAtUtc",
                schema: "inventory",
                table: "stock_adjustments",
                columns: new[] { "ProductId", "CreatedAtUtc" });
        }

        /// <summary>Removes the schema additions when rolling back this migration.</summary>
        /// <param name="migrationBuilder">Migration operation builder.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_adjustments",
                schema: "inventory");
        }
    }
}
