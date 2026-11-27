using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExercisePerSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PerSide",
                schema: "sports",
                table: "exercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerSide",
                schema: "sports",
                table: "exercises");
        }
    }
}
