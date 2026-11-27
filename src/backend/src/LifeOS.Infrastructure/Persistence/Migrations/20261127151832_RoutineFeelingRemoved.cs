using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoutineFeelingRemoved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_settings",
                schema: "sports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_completion_discomfort",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_completion_effort",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "sports",
                table: "routine_sessions");

            migrationBuilder.DropColumn(
                name: "Discomfort",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropColumn(
                name: "Effort",
                schema: "sports",
                table: "exercise_completions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                schema: "sports",
                table: "routine_sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Discomfort",
                schema: "sports",
                table: "exercise_completions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Effort",
                schema: "sports",
                table: "exercise_completions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "profile_settings",
                schema: "sports",
                columns: table => new
                {
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultDiscomfort = table.Column<int>(type: "integer", nullable: false),
                    DefaultEffort = table.Column<int>(type: "integer", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_settings", x => x.MemberProfileId);
                    table.CheckConstraint("ck_sport_settings_discomfort", "\"DefaultDiscomfort\" >= 0 AND \"DefaultDiscomfort\" <= 10");
                    table.CheckConstraint("ck_sport_settings_effort", "\"DefaultEffort\" >= 1 AND \"DefaultEffort\" <= 10");
                    table.ForeignKey(
                        name: "FK_profile_settings_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_profile_settings_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_exercise_completion_discomfort",
                schema: "sports",
                table: "exercise_completions",
                sql: "\"Discomfort\" IS NULL OR (\"Discomfort\" >= 0 AND \"Discomfort\" <= 10)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exercise_completion_effort",
                schema: "sports",
                table: "exercise_completions",
                sql: "\"Effort\" IS NULL OR (\"Effort\" >= 1 AND \"Effort\" <= 10)");

            migrationBuilder.CreateIndex(
                name: "IX_profile_settings_HouseholdId",
                schema: "sports",
                table: "profile_settings",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_profile_settings_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "profile_settings",
                columns: new[] { "MemberProfileId", "HouseholdId" });
        }
    }
}
