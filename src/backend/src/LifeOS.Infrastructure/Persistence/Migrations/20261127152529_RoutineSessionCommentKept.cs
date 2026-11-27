using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoutineSessionCommentKept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RoutineFeelingRemoved dropped this column in its first published version and was then changed to keep it:
            // add it back only where that first version already ran.
            migrationBuilder.Sql("ALTER TABLE sports.routine_sessions ADD COLUMN IF NOT EXISTS \"Comment\" character varying(1000) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "sports",
                table: "routine_sessions");
        }
    }
}
