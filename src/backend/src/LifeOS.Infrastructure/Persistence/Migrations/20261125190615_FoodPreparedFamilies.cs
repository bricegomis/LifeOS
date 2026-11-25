using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FoodPreparedFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_food_family",
                schema: "food",
                table: "foods");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_family",
                schema: "food",
                table: "foods",
                sql: "family IN ('vegetable', 'fruit', 'grain', 'legume', 'meat', 'fish', 'egg', 'dairy', 'fat', 'nut_seed', 'herb_spice', 'condiment', 'sweet', 'prepared', 'dessert', 'drink', 'other')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_food_family",
                schema: "food",
                table: "foods");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_family",
                schema: "food",
                table: "foods",
                sql: "family IN ('vegetable', 'fruit', 'grain', 'legume', 'meat', 'fish', 'egg', 'dairy', 'fat', 'nut_seed', 'herb_spice', 'condiment', 'sweet', 'drink', 'other')");
        }
    }
}
