using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistPlanningConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "frequency_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Target = table.Column<string>(type: "jsonb", nullable: false),
                    TargetCountPerWeek = table.Column<int>(type: "integer", nullable: false)
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
                name: "planning_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Weekday = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MealType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Target = table.Column<string>(type: "jsonb", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_frequency_rules_HouseholdId",
                table: "frequency_rules",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_planning_rules_HouseholdId",
                table: "planning_rules",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_planning_rules_HouseholdId_Weekday_MealType",
                table: "planning_rules",
                columns: new[] { "HouseholdId", "Weekday", "MealType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "frequency_rules");

            migrationBuilder.DropTable(
                name: "planning_rules");

            migrationBuilder.DropTable(
                name: "week_contexts");
        }
    }
}
