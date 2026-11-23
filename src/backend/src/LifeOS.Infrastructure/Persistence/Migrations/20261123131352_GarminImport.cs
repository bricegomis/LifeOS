using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GarminImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sport_session_source",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sport_session_values",
                schema: "sports",
                table: "sport_sessions");

            // ADR 0019: the 1-10 "ressenti" (1 = very hard) is replaced by the watch's effort and feeling,
            // with no conversion; no session existed yet (user decision, 2026-11-23).
            migrationBuilder.DropColumn(
                name: "Feeling",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.AddColumn<int>(
                name: "ElevationGainMeters",
                schema: "sports",
                table: "sport_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "ExcludedSports",
                schema: "sports",
                table: "weekly_goals",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<decimal>(
                name: "AerobicTrainingEffect",
                schema: "sports",
                table: "sport_sessions",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnaerobicTrainingEffect",
                schema: "sports",
                table: "sport_sessions",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageSpeedKmh",
                schema: "sports",
                table: "sport_sessions",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Effort",
                schema: "sports",
                table: "sport_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndedAt",
                schema: "sports",
                table: "sport_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Feel",
                schema: "sports",
                table: "sport_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GarminActivityId",
                schema: "sports",
                table: "sport_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GarminName",
                schema: "sports",
                table: "sport_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GarminRaw",
                schema: "sports",
                table: "sport_sessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GarminType",
                schema: "sports",
                table: "sport_sessions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "HeartRateZoneSeconds",
                schema: "sports",
                table: "sport_sessions",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SportOverridden",
                schema: "sports",
                table: "sport_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "sports",
                table: "sport_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Sessions logged before start and end times existed: noon in Paris, lasting their duration.
            migrationBuilder.Sql("""
                UPDATE sports.sport_sessions
                SET "StartedAt" = ("Date" + TIME '12:00') AT TIME ZONE 'Europe/Paris',
                    "EndedAt" = (("Date" + TIME '12:00') AT TIME ZONE 'Europe/Paris') + make_interval(mins => "DurationMinutes");
                """);

            migrationBuilder.CreateTable(
                name: "daily_wellness",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SleepStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SleepEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    DeepSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    LightSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    RemSleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    AwakeSeconds = table.Column<int>(type: "integer", nullable: true),
                    SleepScore = table.Column<int>(type: "integer", nullable: true),
                    SleepQuality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RestingHeartRate = table.Column<int>(type: "integer", nullable: true),
                    HrvNightAverage = table.Column<int>(type: "integer", nullable: true),
                    HrvWeeklyAverage = table.Column<int>(type: "integer", nullable: true),
                    HrvBaselineLow = table.Column<int>(type: "integer", nullable: true),
                    HrvBaselineHigh = table.Column<int>(type: "integer", nullable: true),
                    HrvStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Respiration = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    SpO2 = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    BodyBatteryWake = table.Column<int>(type: "integer", nullable: true),
                    BodyBatteryHigh = table.Column<int>(type: "integer", nullable: true),
                    BodyBatteryLow = table.Column<int>(type: "integer", nullable: true),
                    StressAverage = table.Column<int>(type: "integer", nullable: true),
                    Steps = table.Column<int>(type: "integer", nullable: true),
                    Raw = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_wellness", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_wellness_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_daily_wellness_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_daily_wellness_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "garmin_links",
                schema: "households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastErrorAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MfaRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SyncRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReconnectRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MfaCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    MfaCodeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garmin_links", x => x.Id);
                    table.CheckConstraint("ck_garmin_link_state", "\"State\" IN ('never', 'ok', 'mfa_requested', 'code_refused', 'code_expired', 'credentials_refused', 'error')");
                    table.ForeignKey(
                        name: "FK_garmin_links_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_garmin_links_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_tokens",
                schema: "households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Hint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_tokens_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_tokens_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_tokens_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vo2max_measures",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Sport = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vo2max_measures", x => x.Id);
                    table.CheckConstraint("ck_vo2max_sport", "\"Sport\" IN ('running', 'cycling')");
                    table.CheckConstraint("ck_vo2max_value", "\"Value\" >= 10 AND \"Value\" <= 100");
                    table.ForeignKey(
                        name: "FK_vo2max_measures_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vo2max_measures_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sport_sessions_HouseholdId_MemberProfileId_GarminActivityId",
                schema: "sports",
                table: "sport_sessions",
                columns: new[] { "HouseholdId", "MemberProfileId", "GarminActivityId" },
                unique: true,
                filter: "\"GarminActivityId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sport_session_source",
                schema: "sports",
                table: "sport_sessions",
                sql: "(\"Source\" = 'manual' AND \"GarminActivityId\" IS NULL) OR (\"Source\" = 'garmin' AND \"GarminActivityId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sport_session_times",
                schema: "sports",
                table: "sport_sessions",
                sql: "\"EndedAt\" > \"StartedAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sport_session_values",
                schema: "sports",
                table: "sport_sessions",
                sql: "(\"Calories\" IS NULL OR \"Calories\" >= 0) AND (\"DistanceKm\" IS NULL OR \"DistanceKm\" >= 0) AND (\"Effort\" IS NULL OR (\"Effort\" >= 1 AND \"Effort\" <= 10)) AND (\"Feel\" IS NULL OR \"Feel\" IN ('very_weak', 'weak', 'normal', 'strong', 'very_strong'))");

            migrationBuilder.CreateIndex(
                name: "IX_daily_wellness_HouseholdId_OwnerUserId",
                schema: "health",
                table: "daily_wellness",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_wellness_HouseholdId_OwnerUserId_Date",
                schema: "health",
                table: "daily_wellness",
                columns: new[] { "HouseholdId", "OwnerUserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_wellness_MemberProfileId_HouseholdId",
                schema: "health",
                table: "daily_wellness",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_wellness_OwnerUserId",
                schema: "health",
                table: "daily_wellness",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_garmin_links_HouseholdId",
                schema: "households",
                table: "garmin_links",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_garmin_links_UserId",
                schema: "households",
                table: "garmin_links",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_tokens_Hash",
                schema: "households",
                table: "import_tokens",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_tokens_HouseholdId",
                schema: "households",
                table: "import_tokens",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_import_tokens_MemberProfileId_HouseholdId",
                schema: "households",
                table: "import_tokens",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_import_tokens_UserId",
                schema: "households",
                table: "import_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_vo2max_measures_HouseholdId_MemberProfileId_Sport_Date",
                schema: "sports",
                table: "vo2max_measures",
                columns: new[] { "HouseholdId", "MemberProfileId", "Sport", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vo2max_measures_MemberProfileId_HouseholdId",
                schema: "sports",
                table: "vo2max_measures",
                columns: new[] { "MemberProfileId", "HouseholdId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_wellness",
                schema: "health");

            migrationBuilder.DropTable(
                name: "garmin_links",
                schema: "households");

            migrationBuilder.DropTable(
                name: "import_tokens",
                schema: "households");

            migrationBuilder.DropTable(
                name: "vo2max_measures",
                schema: "sports");

            migrationBuilder.DropIndex(
                name: "IX_sport_sessions_HouseholdId_MemberProfileId_GarminActivityId",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sport_session_source",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sport_session_times",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sport_session_values",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "ExcludedSports",
                schema: "sports",
                table: "weekly_goals");

            migrationBuilder.DropColumn(
                name: "AerobicTrainingEffect",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "AnaerobicTrainingEffect",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "AverageSpeedKmh",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "Effort",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "Feel",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "GarminActivityId",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "GarminName",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "GarminRaw",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "GarminType",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "HeartRateZoneSeconds",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "SportOverridden",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.DropColumn(
                name: "ElevationGainMeters",
                schema: "sports",
                table: "sport_sessions");

            migrationBuilder.AddColumn<int>(
                name: "Feeling",
                schema: "sports",
                table: "sport_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sport_session_source",
                schema: "sports",
                table: "sport_sessions",
                sql: "\"Source\" IN ('manual')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sport_session_values",
                schema: "sports",
                table: "sport_sessions",
                sql: "(\"Calories\" IS NULL OR \"Calories\" >= 0) AND (\"DistanceKm\" IS NULL OR \"DistanceKm\" >= 0) AND (\"Feeling\" IS NULL OR (\"Feeling\" >= 1 AND \"Feeling\" <= 10))");
        }
    }
}
