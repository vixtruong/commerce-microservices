using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>Adds Identity-owned access-control audit records.</summary>
    public partial class AddAccessChangeAudit : Migration
    {
        /// <summary>Applies the service-owned schema additions.</summary>
        /// <param name="migrationBuilder">Migration operation builder.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_changes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Before = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    After = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_changes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_changes_CreatedAtUtc",
                schema: "identity",
                table: "access_changes",
                column: "CreatedAtUtc");
        }

        /// <summary>Removes the schema additions when rolling back this migration.</summary>
        /// <param name="migrationBuilder">Migration operation builder.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_changes",
                schema: "identity");
        }
    }
}
