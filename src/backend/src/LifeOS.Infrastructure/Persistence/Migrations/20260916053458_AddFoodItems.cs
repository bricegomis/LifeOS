using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "food_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reference_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    calories_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    proteins_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    carbs_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    fats_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    off_barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    off_payload = table.Column<string>(type: "jsonb", nullable: true),
                    is_correction_of = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_food_items_household_id",
                table: "food_items",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "idx_food_items_household_off_barcode",
                table: "food_items",
                columns: new[] { "household_id", "off_barcode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_items");
        }
    }
}
