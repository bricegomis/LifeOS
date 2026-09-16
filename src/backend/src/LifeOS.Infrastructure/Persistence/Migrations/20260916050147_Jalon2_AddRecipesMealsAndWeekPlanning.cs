using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Jalon2_AddRecipesMealsAndWeekPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "composed_meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                name: "recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Servings = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Metadata = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipes_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weeks_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "composed_meal_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComposedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityFactor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ComposedMealId1 = table.Column<Guid>(type: "uuid", nullable: true)
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
                        name: "FK_composed_meal_parts_composed_meals_ComposedMealId1",
                        column: x => x.ComposedMealId1,
                        principalTable: "composed_meals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_composed_meal_parts_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecipeId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_articles_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_recipes_RecipeId1",
                        column: x => x.RecipeId1,
                        principalTable: "recipes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "day_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkContext = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BikeCommute = table.Column<bool>(type: "boolean", nullable: false),
                    WeekId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_day_plans_weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_day_plans_weeks_WeekId1",
                        column: x => x.WeekId1,
                        principalTable: "weeks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "planned_meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DayPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ComposedMealId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DayPlanId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_planned_meals_composed_meals_ComposedMealId",
                        column: x => x.ComposedMealId,
                        principalTable: "composed_meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_planned_meals_day_plans_DayPlanId",
                        column: x => x.DayPlanId,
                        principalTable: "day_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_planned_meals_day_plans_DayPlanId1",
                        column: x => x.DayPlanId1,
                        principalTable: "day_plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_planned_meals_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "planned_meal_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortionMultiplier = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PlannedMealId1 = table.Column<Guid>(type: "uuid", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_planned_meal_parts_planned_meals_PlannedMealId1",
                        column: x => x.PlannedMealId1,
                        principalTable: "planned_meals",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_composed_meal_parts_ComposedMealId",
                table: "composed_meal_parts",
                column: "ComposedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_composed_meal_parts_ComposedMealId1",
                table: "composed_meal_parts",
                column: "ComposedMealId1");

            migrationBuilder.CreateIndex(
                name: "IX_composed_meal_parts_RecipeId",
                table: "composed_meal_parts",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_composed_meals_HouseholdId",
                table: "composed_meals",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_day_plans_Date",
                table: "day_plans",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_day_plans_WeekId",
                table: "day_plans",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_day_plans_WeekId1",
                table: "day_plans",
                column: "WeekId1");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meal_parts_MemberProfileId",
                table: "planned_meal_parts",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meal_parts_PlannedMealId",
                table: "planned_meal_parts",
                column: "PlannedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meal_parts_PlannedMealId1",
                table: "planned_meal_parts",
                column: "PlannedMealId1");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meals_ComposedMealId",
                table: "planned_meals",
                column: "ComposedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meals_DayPlanId",
                table: "planned_meals",
                column: "DayPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meals_DayPlanId1",
                table: "planned_meals",
                column: "DayPlanId1");

            migrationBuilder.CreateIndex(
                name: "IX_planned_meals_RecipeId",
                table: "planned_meals",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_FoodItemId",
                table: "recipe_ingredients",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_RecipeId",
                table: "recipe_ingredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_RecipeId1",
                table: "recipe_ingredients",
                column: "RecipeId1");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_HouseholdId",
                table: "recipes",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_weeks_HouseholdId",
                table: "weeks",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_weeks_HouseholdId_StartsOn",
                table: "weeks",
                columns: new[] { "HouseholdId", "StartsOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "composed_meal_parts");

            migrationBuilder.DropTable(
                name: "planned_meal_parts");

            migrationBuilder.DropTable(
                name: "recipe_ingredients");

            migrationBuilder.DropTable(
                name: "planned_meals");

            migrationBuilder.DropTable(
                name: "composed_meals");

            migrationBuilder.DropTable(
                name: "day_plans");

            migrationBuilder.DropTable(
                name: "recipes");

            migrationBuilder.DropTable(
                name: "weeks");
        }
    }
}
