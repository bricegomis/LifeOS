using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HealthSupplements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "health");

            migrationBuilder.CreateTable(
                name: "supplements",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Dose = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Moment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PackageContent = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    PackagePrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Shop = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsStopped = table.Column<bool>(type: "boolean", nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CureLength = table.Column<int>(type: "integer", nullable: false),
                    CureUnit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PeriodKind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PauseLength = table.Column<int>(type: "integer", nullable: false),
                    PauseUnit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PeriodStartOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplements", x => x.Id);
                    table.CheckConstraint("ck_supplement_dose", "\"Dose\" > 0 AND \"PackageContent\" > 0");
                    table.CheckConstraint("ck_supplement_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
                    table.CheckConstraint("ck_supplement_moment", "\"Moment\" IN ('matin', 'midi', 'soir', 'choix')");
                    table.CheckConstraint("ck_supplement_period", "\"PeriodKind\" IN ('year', 'cure')");
                    table.CheckConstraint("ck_supplement_price", "\"PackagePrice\" IS NULL OR \"PackagePrice\" >= 0");
                    table.CheckConstraint("ck_supplement_unit", "\"Unit\" IN ('gelule', 'comprime', 'g', 'ml', 'goutte', 'dose')");
                    table.ForeignKey(
                        name: "FK_supplements_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplements_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplements_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplement_intakes",
                schema: "health",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplement_intakes", x => new { x.SupplementId, x.Day });
                    table.ForeignKey(
                        name: "FK_supplement_intakes_supplements_SupplementId",
                        column: x => x.SupplementId,
                        principalSchema: "health",
                        principalTable: "supplements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplement_links",
                schema: "health",
                columns: table => new
                {
                    Position = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplement_links", x => new { x.SupplementId, x.Position });
                    table.ForeignKey(
                        name: "FK_supplement_links_supplements_SupplementId",
                        column: x => x.SupplementId,
                        principalSchema: "health",
                        principalTable: "supplements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplements_HouseholdId_OwnerUserId",
                schema: "health",
                table: "supplements",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplements_MemberProfileId_HouseholdId",
                schema: "health",
                table: "supplements",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplements_OwnerUserId",
                schema: "health",
                table: "supplements",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplement_intakes",
                schema: "health");

            migrationBuilder.DropTable(
                name: "supplement_links",
                schema: "health");

            migrationBuilder.DropTable(
                name: "supplements",
                schema: "health");
        }
    }
}
