using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HealthAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analyses",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Laboratory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_analyses_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analyses_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_analyses_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "markers",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LabMin = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    LabMax = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    TargetMin = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    TargetMax = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    FrequencyMonths = table.Column<int>(type: "integer", nullable: false),
                    panels = table.Column<List<string>>(type: "text[]", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markers", x => x.Id);
                    table.UniqueConstraint("AK_markers_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.CheckConstraint("ck_marker_frequency", "\"FrequencyMonths\" >= 0 AND \"FrequencyMonths\" <= 120");
                    table.ForeignKey(
                        name: "FK_markers_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_markers_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_markers_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "measure_types",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measure_types", x => x.Id);
                    table.UniqueConstraint("AK_measure_types_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.ForeignKey(
                        name: "FK_measure_types_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_measure_types_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_measure_types_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "analysis_values",
                schema: "health",
                columns: table => new
                {
                    MarkerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_values", x => new { x.AnalysisId, x.MarkerId });
                    table.ForeignKey(
                        name: "FK_analysis_values_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalSchema: "health",
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analysis_values_markers_MarkerId",
                        column: x => x.MarkerId,
                        principalSchema: "health",
                        principalTable: "markers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "measurements",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasureTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_measurements_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_measurements_measure_types_MeasureTypeId_OwnerUserId",
                        columns: x => new { x.MeasureTypeId, x.OwnerUserId },
                        principalSchema: "health",
                        principalTable: "measure_types",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_measurements_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_measurements_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analyses_HouseholdId_OwnerUserId",
                schema: "health",
                table: "analyses",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_analyses_MemberProfileId_HouseholdId",
                schema: "health",
                table: "analyses",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_analyses_OwnerUserId",
                schema: "health",
                table: "analyses",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_values_MarkerId",
                schema: "health",
                table: "analysis_values",
                column: "MarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_markers_HouseholdId_OwnerUserId",
                schema: "health",
                table: "markers",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_markers_MemberProfileId_HouseholdId",
                schema: "health",
                table: "markers",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_markers_OwnerUserId",
                schema: "health",
                table: "markers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_measure_types_HouseholdId_OwnerUserId",
                schema: "health",
                table: "measure_types",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_measure_types_MemberProfileId_HouseholdId",
                schema: "health",
                table: "measure_types",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_measure_types_OwnerUserId",
                schema: "health",
                table: "measure_types",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_measurements_HouseholdId_OwnerUserId",
                schema: "health",
                table: "measurements",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_measurements_MeasureTypeId_OwnerUserId",
                schema: "health",
                table: "measurements",
                columns: new[] { "MeasureTypeId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_measurements_MemberProfileId_HouseholdId",
                schema: "health",
                table: "measurements",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_measurements_OwnerUserId",
                schema: "health",
                table: "measurements",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_values",
                schema: "health");

            migrationBuilder.DropTable(
                name: "measurements",
                schema: "health");

            migrationBuilder.DropTable(
                name: "analyses",
                schema: "health");

            migrationBuilder.DropTable(
                name: "markers",
                schema: "health");

            migrationBuilder.DropTable(
                name: "measure_types",
                schema: "health");
        }
    }
}
