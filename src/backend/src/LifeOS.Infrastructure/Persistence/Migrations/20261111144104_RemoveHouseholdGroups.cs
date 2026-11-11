using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHouseholdGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deliberately destructive (ADR 0010): reviews attached to a named group go away
            // with the groups. Reviews attached to a profile are kept.
            migrationBuilder.Sql("DELETE FROM food_reviews WHERE \"MemberProfileId\" IS NULL;");
            migrationBuilder.Sql("DELETE FROM recipe_reviews WHERE \"MemberProfileId\" IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_food_reviews_household_groups_HouseholdGroupId_HouseholdId",
                table: "food_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_reviews_household_groups_HouseholdGroupId_HouseholdId",
                table: "recipe_reviews");

            migrationBuilder.DropTable(
                name: "household_groups");

            migrationBuilder.DropIndex(
                name: "IX_recipe_reviews_HouseholdGroupId_HouseholdId",
                table: "recipe_reviews");

            migrationBuilder.DropIndex(
                name: "IX_recipe_reviews_RecipeId_HouseholdGroupId",
                table: "recipe_reviews");

            migrationBuilder.DropIndex(
                name: "IX_recipe_reviews_RecipeId_MemberProfileId",
                table: "recipe_reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recipe_review_target",
                table: "recipe_reviews");

            migrationBuilder.DropIndex(
                name: "IX_food_reviews_FoodItemId_HouseholdGroupId",
                table: "food_reviews");

            migrationBuilder.DropIndex(
                name: "IX_food_reviews_FoodItemId_MemberProfileId",
                table: "food_reviews");

            migrationBuilder.DropIndex(
                name: "IX_food_reviews_HouseholdGroupId_HouseholdId",
                table: "food_reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_review_target",
                table: "food_reviews");

            migrationBuilder.DropColumn(
                name: "HouseholdGroupId",
                table: "recipe_reviews");

            migrationBuilder.DropColumn(
                name: "HouseholdGroupId",
                table: "food_reviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "MemberProfileId",
                table: "recipe_reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MemberProfileId",
                table: "food_reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_MemberProfileId",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "MemberProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_FoodItemId_MemberProfileId",
                table: "food_reviews",
                columns: new[] { "FoodItemId", "MemberProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_recipe_reviews_RecipeId_MemberProfileId",
                table: "recipe_reviews");

            migrationBuilder.DropIndex(
                name: "IX_food_reviews_FoodItemId_MemberProfileId",
                table: "food_reviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "MemberProfileId",
                table: "recipe_reviews",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdGroupId",
                table: "recipe_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MemberProfileId",
                table: "food_reviews",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdGroupId",
                table: "food_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "household_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_HouseholdGroupId_HouseholdId",
                table: "recipe_reviews",
                columns: new[] { "HouseholdGroupId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_HouseholdGroupId",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "HouseholdGroupId" },
                unique: true,
                filter: "\"RecipeId\" IS NOT NULL AND \"HouseholdGroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_MemberProfileId",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "MemberProfileId" },
                unique: true,
                filter: "\"RecipeId\" IS NOT NULL AND \"MemberProfileId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recipe_review_target",
                table: "recipe_reviews",
                sql: "(\"MemberProfileId\" IS NULL) <> (\"HouseholdGroupId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_FoodItemId_HouseholdGroupId",
                table: "food_reviews",
                columns: new[] { "FoodItemId", "HouseholdGroupId" },
                unique: true,
                filter: "\"HouseholdGroupId\" IS NOT NULL");

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_review_target",
                table: "food_reviews",
                sql: "(\"MemberProfileId\" IS NULL) <> (\"HouseholdGroupId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_household_groups_HouseholdId_Name",
                table: "household_groups",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_food_reviews_household_groups_HouseholdGroupId_HouseholdId",
                table: "food_reviews",
                columns: new[] { "HouseholdGroupId", "HouseholdId" },
                principalTable: "household_groups",
                principalColumns: new[] { "Id", "HouseholdId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_reviews_household_groups_HouseholdGroupId_HouseholdId",
                table: "recipe_reviews",
                columns: new[] { "HouseholdGroupId", "HouseholdId" },
                principalTable: "household_groups",
                principalColumns: new[] { "Id", "HouseholdId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
