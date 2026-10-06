using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                schema: "catalog",
                table: "products",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategorySlug",
                schema: "catalog",
                table: "products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "catalog",
                table: "products",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                schema: "catalog",
                table: "products",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_categories_Slug",
                schema: "catalog",
                table: "categories",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_products_CategorySlug",
                schema: "catalog",
                table: "products",
                column: "CategorySlug");

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_CategorySlug",
                schema: "catalog",
                table: "products",
                column: "CategorySlug",
                principalSchema: "catalog",
                principalTable: "categories",
                principalColumn: "Slug",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_CategorySlug",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_CategorySlug",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_categories_Slug",
                schema: "catalog",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "Brand",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CategorySlug",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                schema: "catalog",
                table: "products");
        }
    }
}
