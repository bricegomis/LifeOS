using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskSubProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                schema: "tasks",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_ParentId_HouseholdId",
                schema: "tasks",
                table: "projects",
                columns: new[] { "ParentId", "HouseholdId" });

            migrationBuilder.AddForeignKey(
                name: "FK_projects_projects_ParentId_HouseholdId",
                schema: "tasks",
                table: "projects",
                columns: new[] { "ParentId", "HouseholdId" },
                principalSchema: "tasks",
                principalTable: "projects",
                principalColumns: new[] { "Id", "HouseholdId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_projects_projects_ParentId_HouseholdId",
                schema: "tasks",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_ParentId_HouseholdId",
                schema: "tasks",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "tasks",
                table: "projects");
        }
    }
}
