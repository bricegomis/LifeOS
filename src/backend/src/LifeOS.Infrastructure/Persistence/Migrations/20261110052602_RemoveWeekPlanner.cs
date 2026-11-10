using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Removes the manual week planner (weeks, days, planned meals and lines, activity sessions).
    /// Reviews of ad-hoc planned lines are deleted with them; recipe reviews now always target a recipe.
    /// Sport templates are kept. Legacy unmapped tables that referenced weeks or days (for example
    /// <c>shopping_list_items</c>) keep their rows; only their foreign keys are dropped by CASCADE.
    /// </summary>
    public partial class RemoveWeekPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM recipe_reviews WHERE "PlannedRecipeLineId" IS NOT NULL;""");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_reviews_planned_recipe_lines_PlannedRecipeLineId_Hou~",
                table: "recipe_reviews");

            migrationBuilder.DropIndex(
                name: "IX_recipe_reviews_PlannedRecipeLineId_HouseholdGroupId",
                table: "recipe_reviews");

            migrationBuilder.DropIndex(
                name: "IX_recipe_reviews_PlannedRecipeLineId_HouseholdId",
                table: "recipe_reviews");

            migrationBuilder.DropIndex(
                name: "IX_recipe_reviews_PlannedRecipeLineId_MemberProfileId",
                table: "recipe_reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recipe_review_source",
                table: "recipe_reviews");

            migrationBuilder.DropColumn(
                name: "PlannedRecipeLineId",
                table: "recipe_reviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipeId",
                table: "recipe_reviews",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS
                    activity_sessions,
                    planned_recipe_line_ingredients,
                    planned_recipe_line_portions,
                    planned_recipe_lines,
                    planned_meals,
                    day_plans,
                    weeks
                CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Le semainier supprimé ne se recrée pas automatiquement. Restaurez une sauvegarde pré-migration.");
        }
    }
}
