using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualTimedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "weeks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "weeks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Europe/Paris");

            migrationBuilder.AddColumn<int>(
                name: "ChildrenCount",
                table: "planned_meals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentName",
                table: "planned_meals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndMinute",
                table: "planned_meals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalPortion",
                table: "planned_meals",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "StartMinute",
                table: "planned_meals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceKm",
                table: "activity_sessions",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndMinute",
                table: "activity_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "activity_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SportTemplateId",
                table: "activity_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartMinute",
                table: "activity_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "meal_food_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Calories = table.Column<double>(type: "double precision", nullable: true),
                    Protein = table.Column<double>(type: "double precision", nullable: true),
                    Carbs = table.Column<double>(type: "double precision", nullable: true),
                    Fat = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_food_lines", x => x.Id);
                    table.CheckConstraint("ck_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_meal_food_lines_food_items_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "food_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meal_food_lines_planned_meals_PlannedMealId",
                        column: x => x.PlannedMealId,
                        principalTable: "planned_meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weeks_HouseholdId_StartsOn_IsManual",
                table: "weeks",
                columns: new[] { "HouseholdId", "StartsOn", "IsManual" },
                unique: true,
                filter: "\"IsManual\" = true");

            migrationBuilder.AddCheckConstraint(
                name: "ck_meal_portions",
                table: "planned_meals",
                sql: "\"PersonalPortion\" > 0 AND \"ChildrenCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_meal_time",
                table: "planned_meals",
                sql: "(\"StartMinute\" IS NULL AND \"EndMinute\" IS NULL) OR (\"StartMinute\" IS NOT NULL AND \"EndMinute\" IS NOT NULL AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"EndMinute\" > \"StartMinute\")");

            migrationBuilder.CreateIndex(
                name: "IX_activity_sessions_SportTemplateId",
                table: "activity_sessions",
                column: "SportTemplateId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activity_time",
                table: "activity_sessions",
                sql: "(\"StartMinute\" IS NULL AND \"EndMinute\" IS NULL) OR (\"StartMinute\" IS NOT NULL AND \"EndMinute\" IS NOT NULL AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"EndMinute\" > \"StartMinute\")");

            migrationBuilder.CreateIndex(
                name: "IX_meal_food_lines_FoodItemId",
                table: "meal_food_lines",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_meal_food_lines_PlannedMealId",
                table: "meal_food_lines",
                column: "PlannedMealId");

            migrationBuilder.AddForeignKey(
                name: "FK_activity_sessions_sport_templates_SportTemplateId",
                table: "activity_sessions",
                column: "SportTemplateId",
                principalTable: "sport_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_sessions_sport_templates_SportTemplateId",
                table: "activity_sessions");

            migrationBuilder.DropTable(
                name: "meal_food_lines");

            migrationBuilder.DropIndex(
                name: "IX_weeks_HouseholdId_StartsOn_IsManual",
                table: "weeks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_meal_portions",
                table: "planned_meals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_meal_time",
                table: "planned_meals");

            migrationBuilder.DropIndex(
                name: "IX_activity_sessions_SportTemplateId",
                table: "activity_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activity_time",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "IsManual",
                table: "weeks");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "weeks");

            migrationBuilder.DropColumn(
                name: "ChildrenCount",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "ContentName",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "EndMinute",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "PersonalPortion",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "StartMinute",
                table: "planned_meals");

            migrationBuilder.DropColumn(
                name: "DistanceKm",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "EndMinute",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "SportTemplateId",
                table: "activity_sessions");

            migrationBuilder.DropColumn(
                name: "StartMinute",
                table: "activity_sessions");
        }
    }
}
