using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeFoodItemForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "source",
                table: "food_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "idx_food_items_is_correction_of",
                table: "food_items",
                column: "is_correction_of");

            migrationBuilder.AddForeignKey(
                name: "FK_food_items_food_items_is_correction_of",
                table: "food_items",
                column: "is_correction_of",
                principalTable: "food_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_food_items_households_household_id",
                table: "food_items",
                column: "household_id",
                principalTable: "households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_food_items_food_items_is_correction_of",
                table: "food_items");

            migrationBuilder.DropForeignKey(
                name: "FK_food_items_households_household_id",
                table: "food_items");

            migrationBuilder.DropIndex(
                name: "idx_food_items_is_correction_of",
                table: "food_items");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                table: "food_items",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
