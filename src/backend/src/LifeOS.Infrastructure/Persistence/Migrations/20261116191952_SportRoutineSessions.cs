using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SportRoutineSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR 0015: an exercise has an optional usual load instead of a "with load" flag; 0 now means bodyweight (null).
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultLoadKg",
                schema: "sports",
                table: "exercises",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "WithLoad",
                schema: "sports",
                table: "exercises");

            migrationBuilder.Sql("""UPDATE sports.routine_lines SET "LoadKg" = NULL WHERE "LoadKg" = 0;""");
            migrationBuilder.Sql("""UPDATE sports.exercise_completions SET "LoadKg" = NULL WHERE "LoadKg" = 0;""");

            migrationBuilder.CreateTable(
                name: "profile_settings",
                schema: "sports",
                columns: table => new
                {
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultEffort = table.Column<int>(type: "integer", nullable: false),
                    DefaultDiscomfort = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "routine_sessions",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_sessions", x => x.Id);
                    table.UniqueConstraint("AK_routine_sessions_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_routine_sessions_routines_RoutineId_HouseholdId",
                        columns: x => new { x.RoutineId, x.HouseholdId },
                        principalSchema: "sports",
                        principalTable: "routines",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetsDone",
                schema: "sports",
                table: "exercise_completions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Existing ticks: one finished-or-not pass per routine and per day, started at the first tick. Each tick
            // counts every set of the target of that day, so streaks, records and calendars stay the same.
            migrationBuilder.Sql("""
                INSERT INTO sports.routine_sessions ("Id", "HouseholdId", "RoutineId", "Date", "StartedAt", "Comment")
                SELECT gen_random_uuid(), c."HouseholdId", c."RoutineId", c."Date", min(c."LoggedAt"), NULL
                FROM sports.exercise_completions c
                GROUP BY c."HouseholdId", c."RoutineId", c."Date";

                UPDATE sports.exercise_completions c SET "SessionId" = s."Id"
                FROM sports.routine_sessions s
                WHERE s."RoutineId" = c."RoutineId" AND s."HouseholdId" = c."HouseholdId" AND s."Date" = c."Date";

                UPDATE sports.exercise_completions c SET "SetsDone" = COALESCE((
                    SELECT l."Sets" FROM sports.routine_lines l
                    WHERE l."RoutineId" = c."RoutineId" AND l."ExerciseId" = c."ExerciseId" AND l."ValidFrom" = COALESCE(
                        (SELECT max(v."ValidFrom") FROM sports.routine_versions v WHERE v."RoutineId" = c."RoutineId" AND v."ValidFrom" <= c."Date"),
                        (SELECT min(v."ValidFrom") FROM sports.routine_versions v WHERE v."RoutineId" = c."RoutineId"))), 1);
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_exercise_completions_RoutineId_ExerciseId_Date",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exercise_default_load",
                schema: "sports",
                table: "exercises",
                sql: "\"DefaultLoadKg\" IS NULL OR (\"DefaultLoadKg\" > 0 AND \"DefaultLoadKg\" <= 500)");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_RoutineId_Date",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "RoutineId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_SessionId_ExerciseId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "SessionId", "ExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_SessionId_HouseholdId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "SessionId", "HouseholdId" });

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_exercise_completion_sets",
                schema: "sports",
                table: "exercise_completions",
                sql: "\"SetsDone\" >= 1 AND \"SetsDone\" <= 20");

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

            migrationBuilder.CreateIndex(
                name: "IX_routine_sessions_RoutineId_Date",
                schema: "sports",
                table: "routine_sessions",
                columns: new[] { "RoutineId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_routine_sessions_RoutineId_HouseholdId",
                schema: "sports",
                table: "routine_sessions",
                columns: new[] { "RoutineId", "HouseholdId" });

            migrationBuilder.AddForeignKey(
                name: "FK_exercise_completions_routine_sessions_SessionId_HouseholdId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "SessionId", "HouseholdId" },
                principalSchema: "sports",
                principalTable: "routine_sessions",
                principalColumns: new[] { "Id", "HouseholdId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exercise_completions_routine_sessions_SessionId_HouseholdId",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropTable(
                name: "profile_settings",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "routine_sessions",
                schema: "sports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_default_load",
                schema: "sports",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_exercise_completions_RoutineId_Date",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropIndex(
                name: "IX_exercise_completions_SessionId_ExerciseId",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropIndex(
                name: "IX_exercise_completions_SessionId_HouseholdId",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_completion_discomfort",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_completion_effort",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_completion_sets",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropColumn(
                name: "DefaultLoadKg",
                schema: "sports",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "Discomfort",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropColumn(
                name: "Effort",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropColumn(
                name: "SetsDone",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.AddColumn<bool>(
                name: "WithLoad",
                schema: "sports",
                table: "exercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // One tick per exercise and per day again: the latest pass of a day wins.
            migrationBuilder.Sql("""
                UPDATE sports.exercises e SET "WithLoad" = TRUE
                WHERE EXISTS (SELECT 1 FROM sports.routine_lines l WHERE l."ExerciseId" = e."Id" AND l."LoadKg" IS NOT NULL);

                DELETE FROM sports.exercise_completions c
                USING sports.exercise_completions d
                WHERE c."RoutineId" = d."RoutineId" AND c."ExerciseId" = d."ExerciseId" AND c."Date" = d."Date"
                  AND (c."LoggedAt", c."Id") < (d."LoggedAt", d."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_RoutineId_ExerciseId_Date",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "RoutineId", "ExerciseId", "Date" },
                unique: true);
        }
    }
}
