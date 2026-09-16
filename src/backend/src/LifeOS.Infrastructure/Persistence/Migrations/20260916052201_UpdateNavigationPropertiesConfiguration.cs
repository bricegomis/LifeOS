using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNavigationPropertiesConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_composed_meal_parts_composed_meals_ComposedMealId1",
                table: "composed_meal_parts");

            migrationBuilder.DropForeignKey(
                name: "FK_day_plans_weeks_WeekId1",
                table: "day_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_planned_meal_parts_planned_meals_PlannedMealId1",
                table: "planned_meal_parts");

            migrationBuilder.DropForeignKey(
                name: "FK_planned_meals_day_plans_DayPlanId1",
                table: "planned_meals");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_ingredients_recipes_RecipeId1",
                table: "recipe_ingredients");

            migrationBuilder.DropIndex(
                name: "IX_recipe_ingredients_RecipeId1",
                table: "recipe_ingredients");

            migrationBuilder.DropIndex(
                name: "IX_planned_meals_DayPlanId1",
                table: "planned_meals");

            migrationBuilder.DropIndex(
                name: "IX_planned_meal_parts_PlannedMealId1",
                table: "planned_meal_parts");

            migrationBuilder.DropIndex(
                name: "IX_day_plans_WeekId1",
                table: "day_plans");

            migrationBuilder.DropIndex(
                name: "IX_composed_meal_parts_ComposedMealId1",
                table: "composed_meal_parts");

            migrationBuilder.DropColumn(
                name: "RecipeId1",
                table: "recipe_ingredients");

            migrationBuilder.DropColumn(
                name: "DayPlanId1",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "PlannedMealId1",
                table: "planned_meal_parts");

            migrationBuilder.DropColumn(
                name: "WeekId1",
                table: "day_plans");

            migrationBuilder.DropColumn(
                name: "ComposedMealId1",
                table: "composed_meal_parts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId1",
                table: "recipe_ingredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DayPlanId1",
                table: "planned_meals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlannedMealId1",
                table: "planned_meal_parts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WeekId1",
                table: "day_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComposedMealId1",
                table: "composed_meal_parts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_RecipeId1",
                table: "recipe_ingredients",
                column: "RecipeId1");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meals_DayPlanId1",
                table: "planned_meals",
                column: "DayPlanId1");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meal_parts_PlannedMealId1",
                table: "planned_meal_parts",
                column: "PlannedMealId1");

            migrationBuilder.CreateIndex(
                name: "IX_day_plans_WeekId1",
                table: "day_plans",
                column: "WeekId1");

            migrationBuilder.CreateIndex(
                name: "IX_composed_meal_parts_ComposedMealId1",
                table: "composed_meal_parts",
                column: "ComposedMealId1");

            migrationBuilder.AddForeignKey(
                name: "FK_composed_meal_parts_composed_meals_ComposedMealId1",
                table: "composed_meal_parts",
                column: "ComposedMealId1",
                principalTable: "composed_meals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_day_plans_weeks_WeekId1",
                table: "day_plans",
                column: "WeekId1",
                principalTable: "weeks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_planned_meal_parts_planned_meals_PlannedMealId1",
                table: "planned_meal_parts",
                column: "PlannedMealId1",
                principalTable: "planned_meals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_planned_meals_day_plans_DayPlanId1",
                table: "planned_meals",
                column: "DayPlanId1",
                principalTable: "day_plans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_ingredients_recipes_RecipeId1",
                table: "recipe_ingredients",
                column: "RecipeId1",
                principalTable: "recipes",
                principalColumn: "Id");
        }
    }
}
