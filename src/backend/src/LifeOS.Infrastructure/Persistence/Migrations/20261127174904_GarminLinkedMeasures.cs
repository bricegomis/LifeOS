using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GarminLinkedMeasures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "health",
                table: "measure_types",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // The resting heart rate measure seeded by LifeOS itself follows Garmin's value from now on.
            migrationBuilder.Sql("""
                UPDATE health.measure_types SET "Source" = 'garmin_resting_heart_rate'
                WHERE "Name" = 'Fréquence cardiaque au repos' AND "Unit" = 'bpm';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                schema: "health",
                table: "measure_types");
        }
    }
}
