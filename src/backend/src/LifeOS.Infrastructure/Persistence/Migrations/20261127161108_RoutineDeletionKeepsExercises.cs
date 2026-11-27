using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoutineDeletionKeepsExercises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Routines are no longer stopped: the stopped ones run again from now (the stop day is dropped).
            migrationBuilder.DropCheckConstraint(
                name: "ck_routine_period",
                schema: "sports",
                table: "routines");

            migrationBuilder.DropColumn(
                name: "StoppedOn",
                schema: "sports",
                table: "routines");

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoutineId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // The exercises done become the profile's own history, kept when a routine is deleted.
            migrationBuilder.AddColumn<Guid>(
                name: "MemberProfileId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE sports.exercise_completions c SET "MemberProfileId" = r."MemberProfileId"
                FROM sports.routines r WHERE r."Id" = c."RoutineId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "MemberProfileId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_MemberProfileId_ExerciseId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "MemberProfileId", "ExerciseId" });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_exercise_completion_pass",
                schema: "sports",
                table: "exercise_completions",
                sql: "(\"RoutineId\" IS NULL) = (\"SessionId\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_exercise_completions_member_profiles_MemberProfileId_Househ~",
                schema: "sports",
                table: "exercise_completions",
                columns: new[] { "MemberProfileId", "HouseholdId" },
                principalSchema: "households",
                principalTable: "member_profiles",
                principalColumns: new[] { "Id", "HouseholdId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exercise_completions_member_profiles_MemberProfileId_Househ~",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropIndex(
                name: "IX_exercise_completions_MemberProfileId_ExerciseId",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropIndex(
                name: "IX_exercise_completions_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercise_completion_pass",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.DropColumn(
                name: "MemberProfileId",
                schema: "sports",
                table: "exercise_completions");

            migrationBuilder.AddColumn<DateOnly>(
                name: "StoppedOn",
                schema: "sports",
                table: "routines",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoutineId",
                schema: "sports",
                table: "exercise_completions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_routine_period",
                schema: "sports",
                table: "routines",
                sql: "\"StoppedOn\" IS NULL OR \"StoppedOn\" >= \"StartedOn\"");
        }
    }
}
