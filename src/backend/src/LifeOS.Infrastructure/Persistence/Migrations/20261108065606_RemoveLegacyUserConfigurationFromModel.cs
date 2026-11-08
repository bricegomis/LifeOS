using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Removes the legacy <c>UserConfiguration</c> entity from the EF model only. The
    /// <c>user_configurations</c> table and its rows are intentionally kept in the database:
    /// migrations stay additive and rollback is done by restoring a backup.
    /// </summary>
    public partial class RemoveLegacyUserConfigurationFromModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the table is no longer mapped but is not dropped.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: Up does not change the schema.
        }
    }
}
