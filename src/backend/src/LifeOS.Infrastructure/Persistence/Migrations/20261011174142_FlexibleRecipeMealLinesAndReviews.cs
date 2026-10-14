using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlexibleRecipeMealLinesAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM planned_meals; DELETE FROM recipes;");

            migrationBuilder.DropForeignKey(
                name: "FK_planned_meals_composed_meals_ComposedMealId",
                table: "planned_meals");

            migrationBuilder.DropForeignKey(
                name: "FK_planned_meals_recipes_RecipeId",
                table: "planned_meals");

            migrationBuilder.DropTable(
                name: "composed_meal_parts");

            migrationBuilder.DropTable(
                name: "frequency_rules");

            migrationBuilder.DropTable(
                name: "meal_food_lines");

            migrationBuilder.DropTable(
                name: "planned_meal_parts");

            migrationBuilder.DropTable(
                name: "planning_rules");

            migrationBuilder.DropTable(
                name: "week_contexts");

            migrationBuilder.DropTable(
                name: "week_scenarios");

            migrationBuilder.DropTable(
                name: "composed_meals");

            migrationBuilder.DropIndex(
                name: "IX_planned_meals_ComposedMealId",
                table: "planned_meals");

            migrationBuilder.DropIndex(
                name: "IX_planned_meals_RecipeId",
                table: "planned_meals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_meal_portions",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "weeks");

            migrationBuilder.DropColumn(
                name: "WeekMode",
                table: "weeks");

            migrationBuilder.DropColumn(
                name: "ChildrenCount",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "ComposedMealId",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "ContentName",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "MealType",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "PersonalPortion",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "planned_meals");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "recipes",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "planned_meals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_recipes_Id_HouseholdId",
                table: "recipes",
                columns: new[] { "Id", "HouseholdId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_planned_meals_Id_HouseholdId",
                table: "planned_meals",
                columns: new[] { "Id", "HouseholdId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_member_profiles_Id_HouseholdId",
                table: "member_profiles",
                columns: new[] { "Id", "HouseholdId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_household_members_SupabaseUserId_HouseholdId",
                table: "household_members",
                columns: new[] { "SupabaseUserId", "HouseholdId" });

            migrationBuilder.CreateTable(
                name: "household_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_groups", x => x.Id);
                    table.UniqueConstraint("AK_household_groups_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_household_groups_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planned_recipe_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    YieldServings = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    PlannedServings = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_recipe_lines", x => x.Id);
                    table.UniqueConstraint("AK_planned_recipe_lines_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_planned_recipe_line_portions", "\"PlannedServings\" IS NULL OR \"PlannedServings\" > 0");
                    table.CheckConstraint("ck_planned_recipe_line_source", "(\"RecipeId\" IS NOT NULL AND \"Name\" IS NOT NULL) OR (\"RecipeId\" IS NULL)");
                    table.CheckConstraint("ck_planned_recipe_line_yield", "\"YieldServings\" > 0");
                    table.ForeignKey(
                        name: "FK_planned_recipe_lines_planned_meals_PlannedMealId_HouseholdId",
                        columns: x => new { x.PlannedMealId, x.HouseholdId },
                        principalTable: "planned_meals",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_planned_recipe_lines_recipes_RecipeId_HouseholdId",
                        columns: x => new { x.RecipeId, x.HouseholdId },
                        principalTable: "recipes",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "food_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    HouseholdGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_reviews", x => x.Id);
                    table.UniqueConstraint("AK_food_reviews_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_food_review_content", "\"Score\" IS NOT NULL OR NULLIF(BTRIM(\"Comment\"), '') IS NOT NULL");
                    table.CheckConstraint("ck_food_review_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 10)");
                    table.CheckConstraint("ck_food_review_target", "(\"MemberProfileId\" IS NULL) <> (\"HouseholdGroupId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_food_reviews_foods_FoodItemId_HouseholdId",
                        columns: x => new { x.FoodItemId, x.HouseholdId },
                        principalTable: "foods",
                        principalColumns: new[] { "id", "household_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_food_reviews_household_groups_HouseholdGroupId_HouseholdId",
                        columns: x => new { x.HouseholdGroupId, x.HouseholdId },
                        principalTable: "household_groups",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_food_reviews_household_members_CreatedByUserId_HouseholdId",
                        columns: x => new { x.CreatedByUserId, x.HouseholdId },
                        principalTable: "household_members",
                        principalColumns: new[] { "SupabaseUserId", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_food_reviews_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planned_recipe_line_ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedRecipeLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Calories = table.Column<double>(type: "double precision", nullable: true),
                    Protein = table.Column<double>(type: "double precision", nullable: true),
                    Carbs = table.Column<double>(type: "double precision", nullable: true),
                    Fat = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_recipe_line_ingredients", x => x.Id);
                    table.CheckConstraint("ck_planned_recipe_ingredient_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_planned_recipe_line_ingredients_foods_FoodItemId_HouseholdId",
                        columns: x => new { x.FoodItemId, x.HouseholdId },
                        principalTable: "foods",
                        principalColumns: new[] { "id", "household_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planned_recipe_line_ingredients_planned_recipe_lines_Planne~",
                        columns: x => new { x.PlannedRecipeLineId, x.HouseholdId },
                        principalTable: "planned_recipe_lines",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planned_recipe_line_portions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedRecipeLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Servings = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_recipe_line_portions", x => x.Id);
                    table.CheckConstraint("ck_planned_recipe_line_portion_servings", "\"Servings\" > 0");
                    table.ForeignKey(
                        name: "FK_planned_recipe_line_portions_member_profiles_MemberProfileI~",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planned_recipe_line_portions_planned_recipe_lines_PlannedRe~",
                        columns: x => new { x.PlannedRecipeLineId, x.HouseholdId },
                        principalTable: "planned_recipe_lines",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedRecipeLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    HouseholdGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_reviews", x => x.Id);
                    table.UniqueConstraint("AK_recipe_reviews_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_recipe_review_content", "\"Score\" IS NOT NULL OR NULLIF(BTRIM(\"Comment\"), '') IS NOT NULL");
                    table.CheckConstraint("ck_recipe_review_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 10)");
                    table.CheckConstraint("ck_recipe_review_source", "(\"RecipeId\" IS NULL) <> (\"PlannedRecipeLineId\" IS NULL)");
                    table.CheckConstraint("ck_recipe_review_target", "(\"MemberProfileId\" IS NULL) <> (\"HouseholdGroupId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_recipe_reviews_household_groups_HouseholdGroupId_HouseholdId",
                        columns: x => new { x.HouseholdGroupId, x.HouseholdId },
                        principalTable: "household_groups",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_reviews_household_members_CreatedByUserId_HouseholdId",
                        columns: x => new { x.CreatedByUserId, x.HouseholdId },
                        principalTable: "household_members",
                        principalColumns: new[] { "SupabaseUserId", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_reviews_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_reviews_planned_recipe_lines_PlannedRecipeLineId_Hou~",
                        columns: x => new { x.PlannedRecipeLineId, x.HouseholdId },
                        principalTable: "planned_recipe_lines",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recipe_reviews_recipes_RecipeId_HouseholdId",
                        columns: x => new { x.RecipeId, x.HouseholdId },
                        principalTable: "recipes",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_CreatedByUserId_HouseholdId",
                table: "food_reviews",
                columns: new[] { "CreatedByUserId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_FoodItemId_HouseholdGroupId",
                table: "food_reviews",
                columns: new[] { "FoodItemId", "HouseholdGroupId" },
                unique: true,
                filter: "\"HouseholdGroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_FoodItemId_HouseholdId",
                table: "food_reviews",
                columns: new[] { "FoodItemId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_FoodItemId_MemberProfileId",
                table: "food_reviews",
                columns: new[] { "FoodItemId", "MemberProfileId" },
                unique: true,
                filter: "\"MemberProfileId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_HouseholdGroupId_HouseholdId",
                table: "food_reviews",
                columns: new[] { "HouseholdGroupId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_MemberProfileId_HouseholdId",
                table: "food_reviews",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_household_groups_HouseholdId_Name",
                table: "household_groups",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_line_ingredients_FoodItemId_HouseholdId",
                table: "planned_recipe_line_ingredients",
                columns: new[] { "FoodItemId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_line_ingredients_PlannedRecipeLineId_Househo~",
                table: "planned_recipe_line_ingredients",
                columns: new[] { "PlannedRecipeLineId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_line_portions_MemberProfileId_HouseholdId",
                table: "planned_recipe_line_portions",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_line_portions_PlannedRecipeLineId_HouseholdId",
                table: "planned_recipe_line_portions",
                columns: new[] { "PlannedRecipeLineId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_line_portions_PlannedRecipeLineId_MemberProf~",
                table: "planned_recipe_line_portions",
                columns: new[] { "PlannedRecipeLineId", "MemberProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_lines_Id_HouseholdId",
                table: "planned_recipe_lines",
                columns: new[] { "Id", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_lines_PlannedMealId_HouseholdId",
                table: "planned_recipe_lines",
                columns: new[] { "PlannedMealId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_lines_PlannedMealId_Position",
                table: "planned_recipe_lines",
                columns: new[] { "PlannedMealId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planned_recipe_lines_RecipeId_HouseholdId",
                table: "planned_recipe_lines",
                columns: new[] { "RecipeId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_CreatedByUserId_HouseholdId",
                table: "recipe_reviews",
                columns: new[] { "CreatedByUserId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_HouseholdGroupId_HouseholdId",
                table: "recipe_reviews",
                columns: new[] { "HouseholdGroupId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_MemberProfileId_HouseholdId",
                table: "recipe_reviews",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_PlannedRecipeLineId_HouseholdGroupId",
                table: "recipe_reviews",
                columns: new[] { "PlannedRecipeLineId", "HouseholdGroupId" },
                unique: true,
                filter: "\"PlannedRecipeLineId\" IS NOT NULL AND \"HouseholdGroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_PlannedRecipeLineId_HouseholdId",
                table: "recipe_reviews",
                columns: new[] { "PlannedRecipeLineId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_PlannedRecipeLineId_MemberProfileId",
                table: "recipe_reviews",
                columns: new[] { "PlannedRecipeLineId", "MemberProfileId" },
                unique: true,
                filter: "\"PlannedRecipeLineId\" IS NOT NULL AND \"MemberProfileId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_HouseholdGroupId",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "HouseholdGroupId" },
                unique: true,
                filter: "\"RecipeId\" IS NOT NULL AND \"HouseholdGroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_HouseholdId",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_MemberProfileId",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "MemberProfileId" },
                unique: true,
                filter: "\"RecipeId\" IS NOT NULL AND \"MemberProfileId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_reviews");

            migrationBuilder.DropTable(
                name: "planned_recipe_line_ingredients");

            migrationBuilder.DropTable(
                name: "planned_recipe_line_portions");

            migrationBuilder.DropTable(
                name: "recipe_reviews");

            migrationBuilder.DropTable(
                name: "household_groups");

            migrationBuilder.DropTable(
                name: "planned_recipe_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_recipes_Id_HouseholdId",
                table: "recipes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_planned_meals_Id_HouseholdId",
                table: "planned_meals");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_member_profiles_Id_HouseholdId",
                table: "member_profiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_household_members_SupabaseUserId_HouseholdId",
                table: "household_members");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "planned_meals");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "weeks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WeekMode",
                table: "weeks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ChildrenCount",
                table: "planned_meals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ComposedMealId",
                table: "planned_meals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentName",
                table: "planned_meals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MealType",
                table: "planned_meals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalPortion",
                table: "planned_meals",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId",
                table: "planned_meals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "planned_meals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "composed_meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_composed_meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_composed_meals_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "frequency_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Target = table.Column<string>(type: "jsonb", nullable: false),
                    TargetCountPerWeek = table.Column<int>(type: "integer", nullable: false),
                    WeekMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_frequency_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_frequency_rules_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_food_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Calories = table.Column<double>(type: "double precision", nullable: true),
                    Carbs = table.Column<double>(type: "double precision", nullable: true),
                    Fat = table.Column<double>(type: "double precision", nullable: true),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Protein = table.Column<double>(type: "double precision", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    ReferenceUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_food_lines", x => x.Id);
                    table.CheckConstraint("ck_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_meal_food_lines_foods_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meal_food_lines_planned_meals_PlannedMealId",
                        column: x => x.PlannedMealId,
                        principalTable: "planned_meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planned_meal_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortionMultiplier = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_meal_parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_planned_meal_parts_member_profiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planned_meal_parts_planned_meals_PlannedMealId",
                        column: x => x.PlannedMealId,
                        principalTable: "planned_meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planning_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Target = table.Column<string>(type: "jsonb", nullable: false),
                    WeekMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Weekday = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planning_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_planning_rules_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "week_contexts",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlternatingWeekConfig = table.Column<string>(type: "jsonb", nullable: false),
                    days = table.Column<string>(type: "jsonb", nullable: false),
                    templates = table.Column<string>(type: "jsonb", nullable: false),
                    week_mode_overrides = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_week_contexts", x => x.HouseholdId);
                    table.ForeignKey(
                        name: "FK_week_contexts_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "week_scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Applied = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Explanation = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    RankingObjective = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WeekId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_week_scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_week_scenarios_weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "composed_meal_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComposedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityFactor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_composed_meal_parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_composed_meal_parts_composed_meals_ComposedMealId",
                        column: x => x.ComposedMealId,
                        principalTable: "composed_meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_composed_meal_parts_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_planned_meals_ComposedMealId",
                table: "planned_meals",
                column: "ComposedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meals_RecipeId",
                table: "planned_meals",
                column: "RecipeId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_meal_portions",
                table: "planned_meals",
                sql: "\"PersonalPortion\" > 0 AND \"ChildrenCount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_composed_meal_parts_ComposedMealId",
                table: "composed_meal_parts",
                column: "ComposedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_composed_meal_parts_RecipeId",
                table: "composed_meal_parts",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_composed_meals_HouseholdId",
                table: "composed_meals",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_frequency_rules_HouseholdId",
                table: "frequency_rules",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_meal_food_lines_FoodItemId",
                table: "meal_food_lines",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_meal_food_lines_PlannedMealId",
                table: "meal_food_lines",
                column: "PlannedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meal_parts_MemberProfileId",
                table: "planned_meal_parts",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meal_parts_PlannedMealId",
                table: "planned_meal_parts",
                column: "PlannedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_planning_rules_HouseholdId",
                table: "planning_rules",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_planning_rules_HouseholdId_Weekday_MealType",
                table: "planning_rules",
                columns: new[] { "HouseholdId", "Weekday", "MealType" });

            migrationBuilder.CreateIndex(
                name: "IX_week_scenarios_WeekId",
                table: "week_scenarios",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_week_scenarios_WeekId_RankingObjective",
                table: "week_scenarios",
                columns: new[] { "WeekId", "RankingObjective" });

            migrationBuilder.AddForeignKey(
                name: "FK_planned_meals_composed_meals_ComposedMealId",
                table: "planned_meals",
                column: "ComposedMealId",
                principalTable: "composed_meals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_planned_meals_recipes_RecipeId",
                table: "planned_meals",
                column: "RecipeId",
                principalTable: "recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
