using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SportRoutines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercises",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Measure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WithLoad = table.Column<bool>(type: "boolean", nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.Id);
                    table.UniqueConstraint("AK_exercises_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_exercise_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
                    table.CheckConstraint("ck_exercise_measure", "\"Measure\" IN ('hold', 'reps')");
                    table.ForeignKey(
                        name: "FK_exercises_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "routines",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    StoppedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routines", x => x.Id);
                    table.UniqueConstraint("AK_routines_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_routine_period", "\"StoppedOn\" IS NULL OR \"StoppedOn\" >= \"StartedOn\"");
                    table.ForeignKey(
                        name: "FK_routines_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_routines_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise_completions",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    LoadKg = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    LoggedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_completions", x => x.Id);
                    table.CheckConstraint("ck_exercise_completion_load", "\"LoadKg\" IS NULL OR (\"LoadKg\" >= 0 AND \"LoadKg\" <= 500)");
                    table.CheckConstraint("ck_exercise_completion_value", "\"Value\" >= 1 AND \"Value\" <= 10000");
                    table.ForeignKey(
                        name: "FK_exercise_completions_exercises_ExerciseId_HouseholdId",
                        columns: x => new { x.ExerciseId, x.HouseholdId },
                        principalSchema: "sports",
                        principalTable: "exercises",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_completions_routines_RoutineId_HouseholdId",
                        columns: x => new { x.RoutineId, x.HouseholdId },
                        principalSchema: "sports",
                        principalTable: "routines",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "routine_versions",
                schema: "sports",
                columns: table => new
                {
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    JokersPerWeek = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_versions", x => new { x.RoutineId, x.ValidFrom });
                    table.CheckConstraint("ck_routine_version_days", "\"Days\" >= 1 AND \"Days\" <= 127");
                    table.CheckConstraint("ck_routine_version_jokers", "\"JokersPerWeek\" >= 0 AND \"JokersPerWeek\" <= 2");
                    table.ForeignKey(
                        name: "FK_routine_versions_routines_RoutineId",
                        column: x => x.RoutineId,
                        principalSchema: "sports",
                        principalTable: "routines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "routine_lines",
                schema: "sports",
                columns: table => new
                {
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Sets = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    LoadKg = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_lines", x => new { x.RoutineId, x.ValidFrom, x.ExerciseId });
                    table.CheckConstraint("ck_routine_line_load", "\"LoadKg\" IS NULL OR (\"LoadKg\" >= 0 AND \"LoadKg\" <= 500)");
                    table.CheckConstraint("ck_routine_line_sets", "\"Sets\" >= 1 AND \"Sets\" <= 20");
                    table.CheckConstraint("ck_routine_line_target", "\"Target\" >= 1 AND \"Target\" <= 10000");
                    table.ForeignKey(
                        name: "FK_routine_lines_exercises_ExerciseId_HouseholdId",
                        columns: x => new { x.ExerciseId, x.HouseholdId },
                        principalSchema: "sports",
                        principalTable: "exercises",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_routine_lines_routine_versions_RoutineId_ValidFrom",
                        columns: x => new { x.RoutineId, x.ValidFrom },
                        principalSchema: "sports",
                        principalTable: "routine_versions",
                        principalColumns: new[] { "RoutineId", "ValidFrom" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_ExerciseId_HouseholdId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "ExerciseId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_HouseholdId_ExerciseId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "HouseholdId", "ExerciseId" });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_RoutineId_ExerciseId_Date",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "RoutineId", "ExerciseId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_RoutineId_HouseholdId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "RoutineId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_exercises_HouseholdId",
                schema: "sports",
                table: "exercises",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_routine_lines_ExerciseId_HouseholdId",
                schema: "sports",
                table: "routine_lines",
                columns: new[] { "ExerciseId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_routines_HouseholdId_MemberProfileId",
                schema: "sports",
                table: "routines",
                columns: new[] { "HouseholdId", "MemberProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_routines_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "routines",
                columns: new[] { "MemberProfileId", "HouseholdId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_completions",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "routine_lines",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "exercises",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "routine_versions",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "routines",
                schema: "sports");
        }
    }
}
