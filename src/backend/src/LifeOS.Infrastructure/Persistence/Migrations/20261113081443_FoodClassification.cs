using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <summary>Food classification (ADR 0012): resets the foods, then adds family, roles, conservation,
    /// season months and tags.</summary>
    public partial class FoodClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR 0012: every food now needs a family. The user chose to reset the foods instead of
            // handling a missing family (2026-11-13; 4 foods, no product, recipe or image in production).
            // Rows that point to a food go first: reviews, prices and products, recipe ingredients.
            migrationBuilder.Sql("""
                DELETE FROM food.food_reviews;
                DELETE FROM food.product_prices;
                DELETE FROM food.products;
                DELETE FROM food.recipe_ingredients;
                DELETE FROM food.foods;
                """);

            migrationBuilder.AddColumn<string>(
                name: "conservation",
                schema: "food",
                table: "foods",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "family",
                schema: "food",
                table: "foods",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<List<string>>(
                name: "roles",
                schema: "food",
                table: "foods",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<List<int>>(
                name: "season_months",
                schema: "food",
                table: "foods",
                type: "integer[]",
                nullable: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "tags",
                schema: "food",
                table: "foods",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_conservation",
                schema: "food",
                table: "foods",
                sql: "conservation IS NULL OR conservation IN ('fresh', 'frozen', 'canned', 'dry')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_family",
                schema: "food",
                table: "foods",
                sql: "family IN ('vegetable', 'fruit', 'grain', 'legume', 'meat', 'fish', 'egg', 'dairy', 'fat', 'nut_seed', 'herb_spice', 'condiment', 'sweet', 'drink', 'other')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_roles",
                schema: "food",
                table: "foods",
                sql: "roles <@ ARRAY['protein', 'starch', 'vegetable', 'fat', 'seasoning', 'sweet']::text[]");

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_season_months",
                schema: "food",
                table: "foods",
                sql: "season_months <@ ARRAY[1,2,3,4,5,6,7,8,9,10,11,12]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_food_conservation",
                schema: "food",
                table: "foods");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_family",
                schema: "food",
                table: "foods");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_roles",
                schema: "food",
                table: "foods");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_season_months",
                schema: "food",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "conservation",
                schema: "food",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "family",
                schema: "food",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "roles",
                schema: "food",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "season_months",
                schema: "food",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "food",
                table: "foods");
        }
    }
}
