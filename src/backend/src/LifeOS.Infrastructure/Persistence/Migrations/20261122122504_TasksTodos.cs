using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TasksTodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tasks");

            migrationBuilder.CreateTable(
                name: "todos",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DoneOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_todos", x => x.Id);
                    table.UniqueConstraint("AK_todos_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_todo_done_on", "(\"State\" = 'done') = (\"DoneOn\" IS NOT NULL)");
                    table.CheckConstraint("ck_todo_duration", "\"DurationMinutes\" IS NULL OR (\"DurationMinutes\" >= 1 AND \"DurationMinutes\" <= 1440)");
                    table.CheckConstraint("ck_todo_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
                    table.CheckConstraint("ck_todo_priority", "\"Priority\" IN ('high', 'normal', 'low')");
                    table.CheckConstraint("ck_todo_state", "\"State\" IN ('open', 'done', 'abandoned')");
                    table.ForeignKey(
                        name: "FK_todos_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_todos_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "todo_checklist_items",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    TodoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_todo_checklist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_todo_checklist_items_todos_TodoId",
                        column: x => x.TodoId,
                        principalSchema: "tasks",
                        principalTable: "todos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_todo_checklist_items_TodoId_Position",
                schema: "tasks",
                table: "todo_checklist_items",
                columns: new[] { "TodoId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_todos_HouseholdId_State_DueOn",
                schema: "tasks",
                table: "todos",
                columns: new[] { "HouseholdId", "State", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_todos_MemberProfileId_HouseholdId",
                schema: "tasks",
                table: "todos",
                columns: new[] { "MemberProfileId", "HouseholdId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "todo_checklist_items",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "todos",
                schema: "tasks");
        }
    }
}
