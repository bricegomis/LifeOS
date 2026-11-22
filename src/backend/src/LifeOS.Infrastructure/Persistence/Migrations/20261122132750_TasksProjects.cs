using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TasksProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                schema: "tasks",
                table: "todos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.UniqueConstraint("AK_projects_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_task_project_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
                    table.CheckConstraint("ck_task_project_state", "\"State\" IN ('active', 'done', 'abandoned')");
                    table.ForeignKey(
                        name: "FK_projects_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_todos_ProjectId_HouseholdId",
                schema: "tasks",
                table: "todos",
                columns: new[] { "ProjectId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_projects_HouseholdId_State",
                schema: "tasks",
                table: "projects",
                columns: new[] { "HouseholdId", "State" });

            migrationBuilder.AddForeignKey(
                name: "FK_todos_projects_ProjectId_HouseholdId",
                schema: "tasks",
                table: "todos",
                columns: new[] { "ProjectId", "HouseholdId" },
                principalSchema: "tasks",
                principalTable: "projects",
                principalColumns: new[] { "Id", "HouseholdId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_todos_projects_ProjectId_HouseholdId",
                schema: "tasks",
                table: "todos");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_todos_ProjectId_HouseholdId",
                schema: "tasks",
                table: "todos");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "tasks",
                table: "todos");
        }
    }
}
