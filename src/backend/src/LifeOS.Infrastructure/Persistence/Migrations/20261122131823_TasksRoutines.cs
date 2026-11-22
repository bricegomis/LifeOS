using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TasksRoutines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "routines",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    StartOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ResumedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecurrenceDays = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceDelayDays = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceEvery = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceKind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RecurrenceMonthDay = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceUnit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routines1", x => x.Id);
                    table.UniqueConstraint("AK_routines_Id_HouseholdId1", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_task_routine_days", "\"RecurrenceDays\" >= 0 AND \"RecurrenceDays\" <= 127");
                    table.CheckConstraint("ck_task_routine_delay", "\"RecurrenceDelayDays\" >= 1 AND \"RecurrenceDelayDays\" <= 365");
                    table.CheckConstraint("ck_task_routine_duration", "\"DurationMinutes\" IS NULL OR (\"DurationMinutes\" >= 1 AND \"DurationMinutes\" <= 1440)");
                    table.CheckConstraint("ck_task_routine_every", "\"RecurrenceEvery\" >= 1 AND \"RecurrenceEvery\" <= 365");
                    table.CheckConstraint("ck_task_routine_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
                    table.CheckConstraint("ck_task_routine_kind", "\"RecurrenceKind\" IN ('fixed', 'delay')");
                    table.CheckConstraint("ck_task_routine_month_day", "\"RecurrenceMonthDay\" >= 1 AND \"RecurrenceMonthDay\" <= 31");
                    table.CheckConstraint("ck_task_routine_priority", "\"Priority\" IN ('high', 'normal', 'low')");
                    table.CheckConstraint("ck_task_routine_state", "\"State\" IN ('active', 'paused', 'stopped')");
                    table.CheckConstraint("ck_task_routine_unit", "\"RecurrenceUnit\" IN ('day', 'week', 'month')");
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
                name: "routine_passes",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RoutineId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_passes", x => x.Id);
                    table.CheckConstraint("ck_routine_pass_kind", "\"Kind\" IN ('done', 'skipped')");
                    table.ForeignKey(
                        name: "FK_routine_passes_routines_RoutineId",
                        column: x => x.RoutineId,
                        principalSchema: "tasks",
                        principalTable: "routines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_routine_passes_RoutineId_DueOn",
                schema: "tasks",
                table: "routine_passes",
                columns: new[] { "RoutineId", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_routines_HouseholdId_State",
                schema: "tasks",
                table: "routines",
                columns: new[] { "HouseholdId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_routines_MemberProfileId_HouseholdId1",
                schema: "tasks",
                table: "routines",
                columns: new[] { "MemberProfileId", "HouseholdId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_routines_households_HouseholdId",
                schema: "sports",
                table: "routines");

            migrationBuilder.DropForeignKey(
                name: "FK_routines_member_profiles_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "routines");

            migrationBuilder.DropTable(
                name: "routine_passes",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "routines",
                schema: "tasks");
        }
    }
}
