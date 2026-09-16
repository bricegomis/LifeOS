using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Jalon5WeekScenarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "week_scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    RankingObjective = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Explanation = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Applied = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_week_scenarios_WeekId",
                table: "week_scenarios",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_week_scenarios_WeekId_RankingObjective",
                table: "week_scenarios",
                columns: new[] { "WeekId", "RankingObjective" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "week_scenarios");
        }
    }
}
