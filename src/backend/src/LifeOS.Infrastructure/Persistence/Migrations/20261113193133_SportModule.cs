using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SportModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR 0013 (user decision, 2026-11-13): template sports become a fixed list of codes; the
            // old free-text templates cannot be matched by name, so they are deleted once.
            migrationBuilder.Sql("DELETE FROM sports.sport_templates;");

            migrationBuilder.DropColumn(
                name: "Intensity",
                schema: "sports",
                table: "sport_templates");

            migrationBuilder.AlterColumn<string>(
                name: "Sport",
                schema: "sports",
                table: "sport_templates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "DistanceKm",
                schema: "sports",
                table: "sport_templates",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Calories",
                schema: "sports",
                table: "sport_templates",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_sport_templates_Id_HouseholdId",
                schema: "sports",
                table: "sport_templates",
                columns: new[] { "Id", "HouseholdId" });

            migrationBuilder.CreateTable(
                name: "daily_challenges",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    StoppedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_challenges", x => x.Id);
                    table.UniqueConstraint("AK_daily_challenges_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_daily_challenge_period", "\"StoppedOn\" IS NULL OR \"StoppedOn\" >= \"StartedOn\"");
                    table.CheckConstraint("ck_daily_challenge_unit", "\"Unit\" IN ('reps', 'minutes')");
                    table.ForeignKey(
                        name: "FK_daily_challenges_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_daily_challenges_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sport_sessions",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Sport = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DistanceKm = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    Calories = table.Column<int>(type: "integer", nullable: true),
                    Feeling = table.Column<int>(type: "integer", nullable: true),
                    AverageHeartRate = table.Column<int>(type: "integer", nullable: true),
                    MaxHeartRate = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sport_sessions", x => x.Id);
                    table.CheckConstraint("ck_sport_session_duration", "\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= 1440");
                    table.CheckConstraint("ck_sport_session_source", "\"Source\" IN ('manual')");
                    table.CheckConstraint("ck_sport_session_sport", "\"Sport\" IN ('running', 'walking', 'hiking', 'cycling', 'swimming', 'strength', 'crossfit', 'bodyweight', 'mobility', 'team_sport', 'racket', 'other')");
                    table.CheckConstraint("ck_sport_session_values", "(\"Calories\" IS NULL OR \"Calories\" >= 0) AND (\"DistanceKm\" IS NULL OR \"DistanceKm\" >= 0) AND (\"Feeling\" IS NULL OR (\"Feeling\" >= 1 AND \"Feeling\" <= 10))");
                    table.ForeignKey(
                        name: "FK_sport_sessions_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sport_sessions_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sport_sessions_sport_templates_TemplateId_HouseholdId",
                        columns: x => new { x.TemplateId, x.HouseholdId },
                        principalSchema: "sports",
                        principalTable: "sport_templates",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weekly_goals",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sport = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Target = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_goals", x => x.Id);
                    table.CheckConstraint("ck_weekly_goal_kind", "(\"Kind\" = 'template' AND \"TemplateId\" IS NOT NULL) OR (\"Kind\" = 'sport' AND \"Sport\" IN ('running', 'walking', 'hiking', 'cycling', 'swimming', 'strength', 'crossfit', 'bodyweight', 'mobility', 'team_sport', 'racket', 'other')) OR \"Kind\" = 'sessions' OR (\"Kind\" = 'volume' AND \"Unit\" IN ('kcal', 'km', 'hours'))");
                    table.CheckConstraint("ck_weekly_goal_period", "\"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
                    table.CheckConstraint("ck_weekly_goal_target", "\"Target\" > 0");
                    table.ForeignKey(
                        name: "FK_weekly_goals_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_weekly_goals_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_goals_sport_templates_TemplateId_HouseholdId",
                        columns: x => new { x.TemplateId, x.HouseholdId },
                        principalSchema: "sports",
                        principalTable: "sport_templates",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "challenge_entries",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LoggedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_challenge_entries", x => x.Id);
                    table.CheckConstraint("ck_challenge_entry_quantity", "\"Quantity\" > 0 AND \"Quantity\" <= 10000");
                    table.ForeignKey(
                        name: "FK_challenge_entries_daily_challenges_ChallengeId_HouseholdId",
                        columns: x => new { x.ChallengeId, x.HouseholdId },
                        principalSchema: "sports",
                        principalTable: "daily_challenges",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_challenge_rules",
                schema: "sports",
                columns: table => new
                {
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyTarget = table.Column<int>(type: "integer", nullable: false),
                    JokersPerWeek = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_challenge_rules", x => new { x.ChallengeId, x.ValidFrom });
                    table.CheckConstraint("ck_challenge_rule_jokers", "\"JokersPerWeek\" >= 0 AND \"JokersPerWeek\" <= 2");
                    table.CheckConstraint("ck_challenge_rule_target", "\"DailyTarget\" > 0");
                    table.ForeignKey(
                        name: "FK_daily_challenge_rules_daily_challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalSchema: "sports",
                        principalTable: "daily_challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sport_template_sport",
                schema: "sports",
                table: "sport_templates",
                sql: "\"Sport\" IN ('running', 'walking', 'hiking', 'cycling', 'swimming', 'strength', 'crossfit', 'bodyweight', 'mobility', 'team_sport', 'racket', 'other')");

            migrationBuilder.CreateIndex(
                name: "IX_challenge_entries_ChallengeId_Date",
                schema: "sports",
                table: "challenge_entries",
                columns: new[] { "ChallengeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_challenge_entries_ChallengeId_HouseholdId",
                schema: "sports",
                table: "challenge_entries",
                columns: new[] { "ChallengeId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_challenges_HouseholdId_MemberProfileId",
                schema: "sports",
                table: "daily_challenges",
                columns: new[] { "HouseholdId", "MemberProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_challenges_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "daily_challenges",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_sport_sessions_HouseholdId_MemberProfileId_Date",
                schema: "sports",
                table: "sport_sessions",
                columns: new[] { "HouseholdId", "MemberProfileId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_sport_sessions_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "sport_sessions",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_sport_sessions_TemplateId_HouseholdId",
                schema: "sports",
                table: "sport_sessions",
                columns: new[] { "TemplateId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_goals_HouseholdId_MemberProfileId",
                schema: "sports",
                table: "weekly_goals",
                columns: new[] { "HouseholdId", "MemberProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_goals_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "weekly_goals",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_goals_TemplateId_HouseholdId",
                schema: "sports",
                table: "weekly_goals",
                columns: new[] { "TemplateId", "HouseholdId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challenge_entries",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "daily_challenge_rules",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "sport_sessions",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "weekly_goals",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "daily_challenges",
                schema: "sports");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_sport_templates_Id_HouseholdId",
                schema: "sports",
                table: "sport_templates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sport_template_sport",
                schema: "sports",
                table: "sport_templates");

            migrationBuilder.AlterColumn<string>(
                name: "Sport",
                schema: "sports",
                table: "sport_templates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<decimal>(
                name: "DistanceKm",
                schema: "sports",
                table: "sport_templates",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldPrecision: 8,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Calories",
                schema: "sports",
                table: "sport_templates",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Intensity",
                schema: "sports",
                table: "sport_templates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
