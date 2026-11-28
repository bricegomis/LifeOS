using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReadingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reading");

            migrationBuilder.CreateTable(
                name: "series",
                schema: "reading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalTomes = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series", x => x.Id);
                    table.UniqueConstraint("AK_series_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_book_series_tomes", "\"TotalTomes\" IS NULL OR (\"TotalTomes\" >= 1 AND \"TotalTomes\" <= 999)");
                    table.ForeignKey(
                        name: "FK_series_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "books",
                schema: "reading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Authors = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    genres = table.Column<List<string>>(type: "text[]", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tome = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NotWanted = table.Column<bool>(type: "boolean", nullable: false),
                    NotWantedReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_books", x => x.Id);
                    table.UniqueConstraint("AK_books_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_book_format", "\"Format\" IN ('paper', 'ereader', 'audio', 'comic')");
                    table.CheckConstraint("ck_book_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
                    table.CheckConstraint("ck_book_tome", "\"Tome\" IS NULL OR (\"SeriesId\" IS NOT NULL AND \"Tome\" > 0)");
                    table.ForeignKey(
                        name: "FK_books_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_books_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_books_series_SeriesId_HouseholdId",
                        columns: x => new { x.SeriesId, x.HouseholdId },
                        principalSchema: "reading",
                        principalTable: "series",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "book_quotes",
                schema: "reading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Place = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_book_quotes_books_BookId",
                        column: x => x.BookId,
                        principalSchema: "reading",
                        principalTable: "books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_readings",
                schema: "reading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FinishedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    Review = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StopReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_readings", x => x.Id);
                    table.CheckConstraint("ck_book_reading_dates", "\"StartedOn\" IS NULL OR \"FinishedOn\" IS NULL OR \"FinishedOn\" >= \"StartedOn\"");
                    table.CheckConstraint("ck_book_reading_outcome", "\"Outcome\" IN ('reading', 'read', 'stopped')");
                    table.CheckConstraint("ck_book_reading_rating", "\"Rating\" IS NULL OR (\"Rating\" >= 0 AND \"Rating\" <= 10)");
                    table.ForeignKey(
                        name: "FK_book_readings_books_BookId",
                        column: x => x.BookId,
                        principalSchema: "reading",
                        principalTable: "books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_quotes_BookId_Position",
                schema: "reading",
                table: "book_quotes",
                columns: new[] { "BookId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_readings_BookId_Position",
                schema: "reading",
                table: "book_readings",
                columns: new[] { "BookId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_book_readings_one_in_progress",
                schema: "reading",
                table: "book_readings",
                column: "BookId",
                unique: true,
                filter: "\"Outcome\" = 'reading'");

            migrationBuilder.CreateIndex(
                name: "IX_books_HouseholdId_Title",
                schema: "reading",
                table: "books",
                columns: new[] { "HouseholdId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_books_MemberProfileId_HouseholdId",
                schema: "reading",
                table: "books",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_books_SeriesId_HouseholdId",
                schema: "reading",
                table: "books",
                columns: new[] { "SeriesId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_series_HouseholdId_Name",
                schema: "reading",
                table: "series",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_quotes",
                schema: "reading");

            migrationBuilder.DropTable(
                name: "book_readings",
                schema: "reading");

            migrationBuilder.DropTable(
                name: "books",
                schema: "reading");

            migrationBuilder.DropTable(
                name: "series",
                schema: "reading");
        }
    }
}
