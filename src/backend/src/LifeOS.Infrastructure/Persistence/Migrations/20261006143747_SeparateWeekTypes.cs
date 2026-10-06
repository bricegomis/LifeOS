using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateWeekTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WeekMode",
                table: "weeks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "templates",
                table: "week_contexts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekMode",
                table: "planning_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeekMode",
                table: "frequency_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE week_contexts
                SET templates = jsonb_build_object('kids', days, 'solo', days);

                UPDATE weeks AS w
                SET "WeekMode" = COALESCE(
                    (
                        SELECT CASE lower(override.value->>'mode')
                            WHEN 'kids' THEN 'Kids'
                            ELSE 'Solo'
                        END
                        FROM week_contexts AS c,
                             jsonb_array_elements(c.week_mode_overrides) AS override(value)
                        WHERE c."HouseholdId" = w."HouseholdId"
                          AND override.value->>'weekStartDate' = to_char(w."StartsOn", 'YYYY-MM-DD')
                        LIMIT 1
                    ),
                    (
                        SELECT CASE
                            WHEN MOD(
                                (w."StartsOn" - to_date(c."AlternatingWeekConfig"->>'referenceWeekStartDate', 'YYYY-MM-DD')) / 7,
                                2
                            ) = 0
                            THEN CASE lower(c."AlternatingWeekConfig"->>'referenceWeekMode')
                                WHEN 'kids' THEN 'Kids'
                                ELSE 'Solo'
                            END
                            ELSE CASE lower(c."AlternatingWeekConfig"->>'referenceWeekMode')
                                WHEN 'kids' THEN 'Solo'
                                ELSE 'Kids'
                            END
                        END
                        FROM week_contexts AS c
                        WHERE c."HouseholdId" = w."HouseholdId"
                    ),
                    'Solo'
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "WeekMode",
                table: "weeks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "templates",
                table: "week_contexts",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekMode",
                table: "weeks");

            migrationBuilder.DropColumn(
                name: "templates",
                table: "week_contexts");

            migrationBuilder.DropColumn(
                name: "WeekMode",
                table: "planning_rules");

            migrationBuilder.DropColumn(
                name: "WeekMode",
                table: "frequency_rules");
        }
    }
}
