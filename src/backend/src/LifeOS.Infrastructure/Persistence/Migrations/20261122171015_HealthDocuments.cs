using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HealthDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Who = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.CheckConstraint("ck_health_document_type", "\"Type\" IN ('ordonnance', 'compte_rendu', 'resultat', 'bilan', 'autre')");
                    table.ForeignKey(
                        name: "FK_documents_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_documents_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Who = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Place = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BookingUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReportDocumentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appointments_documents_ReportDocumentId",
                        column: x => x.ReportDocumentId,
                        principalSchema: "health",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointments_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appointments_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "households",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_files",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    StoredName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_files_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "health",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analyses_DocumentId",
                schema: "health",
                table: "analyses",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_HouseholdId_OwnerUserId",
                schema: "health",
                table: "appointments",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_MemberProfileId_HouseholdId",
                schema: "health",
                table: "appointments",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_OwnerUserId",
                schema: "health",
                table: "appointments",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ReportDocumentId",
                schema: "health",
                table: "appointments",
                column: "ReportDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_files_DocumentId",
                schema: "health",
                table: "document_files",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_HouseholdId_OwnerUserId",
                schema: "health",
                table: "documents",
                columns: new[] { "HouseholdId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_MemberProfileId_HouseholdId",
                schema: "health",
                table: "documents",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_OwnerUserId",
                schema: "health",
                table: "documents",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_analyses_documents_DocumentId",
                schema: "health",
                table: "analyses",
                column: "DocumentId",
                principalSchema: "health",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_analyses_documents_DocumentId",
                schema: "health",
                table: "analyses");

            migrationBuilder.DropTable(
                name: "appointments",
                schema: "health");

            migrationBuilder.DropTable(
                name: "document_files",
                schema: "health");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "health");

            migrationBuilder.DropIndex(
                name: "IX_analyses_DocumentId",
                schema: "health",
                table: "analyses");
        }
    }
}
