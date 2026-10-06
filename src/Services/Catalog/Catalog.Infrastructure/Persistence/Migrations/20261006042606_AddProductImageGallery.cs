using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                schema: "catalog",
                table: "products",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
            // Existing single-photo products retain their current primary photo in the new ordered gallery.
            migrationBuilder.Sql("UPDATE catalog.products SET \"ImageUrls\" = jsonb_build_array(\"ImageUrl\") WHERE \"ImageUrl\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrls",
                schema: "catalog",
                table: "products");
        }
    }
}
