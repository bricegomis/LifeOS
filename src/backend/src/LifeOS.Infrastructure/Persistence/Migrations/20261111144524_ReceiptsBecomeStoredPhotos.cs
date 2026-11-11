using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptsBecomeStoredPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchases");

            migrationBuilder.DropTable(
                name: "receipt_lines");

            // Deliberately destructive (ADR 0010): a receipt is now only a stored photo.
            // JSON imports have no photo and are deleted with their lines and purchases;
            // observed prices are kept, without their link to a receipt line.
            migrationBuilder.Sql("DELETE FROM receipts WHERE \"ImagePath\" IS NULL OR \"ImageContentType\" IS NULL;");
            migrationBuilder.Sql("UPDATE receipts SET \"Status\" = CASE WHEN \"Status\" = 'validated' THEN 'processed' ELSE 'pending' END;");

            migrationBuilder.DropIndex(
                name: "IX_article_price_entries_ReceiptLineId",
                table: "article_price_entries");

            migrationBuilder.DropColumn(
                name: "DeclaredLines",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "DetectedLines",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "Diagnostic",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "OcrError",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "OcrText",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "PurchasedAt",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "RawDocument",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "StoreName",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "ReceiptLineId",
                table: "article_price_entries");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "receipts",
                newName: "PurchaseDate");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "receipts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageContentType",
                table: "receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_status",
                table: "receipts",
                sql: "\"Status\" IN ('pending', 'processed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_status",
                table: "receipts");

            migrationBuilder.RenameColumn(
                name: "PurchaseDate",
                table: "receipts",
                newName: "Date");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "receipts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "ImageContentType",
                table: "receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "DeclaredLines",
                table: "receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DetectedLines",
                table: "receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Diagnostic",
                table: "receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrError",
                table: "receipts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrText",
                table: "receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurchasedAt",
                table: "receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawDocument",
                table: "receipts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                table: "receipts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptLineId",
                table: "article_price_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "receipt_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExcludedMention = table.Column<bool>(type: "boolean", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PriceMismatchConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: true),
                    RawSource = table.Column<string>(type: "text", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDiagnostic = table.Column<string>(type: "text", nullable: true),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    StatedUnitPrice = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_lines", x => x.Id);
                    table.UniqueConstraint("AK_receipt_lines_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_receipt_lines_products_ProductId_HouseholdId",
                        columns: x => new { x.ProductId, x.HouseholdId },
                        principalTable: "products",
                        principalColumns: new[] { "id", "household_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_lines_receipts_ReceiptId_HouseholdId",
                        columns: x => new { x.ReceiptId, x.HouseholdId },
                        principalTable: "receipts",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchases_products_ProductId_HouseholdId",
                        columns: x => new { x.ProductId, x.HouseholdId },
                        principalTable: "products",
                        principalColumns: new[] { "id", "household_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_receipt_lines_ReceiptLineId_HouseholdId",
                        columns: x => new { x.ReceiptLineId, x.HouseholdId },
                        principalTable: "receipt_lines",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_stores_StoreId_HouseholdId",
                        columns: x => new { x.StoreId, x.HouseholdId },
                        principalTable: "stores",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_article_price_entries_ReceiptLineId",
                table: "article_price_entries",
                column: "ReceiptLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_ProductId_HouseholdId",
                table: "purchases",
                columns: new[] { "ProductId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_ReceiptLineId",
                table: "purchases",
                column: "ReceiptLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_ReceiptLineId_HouseholdId",
                table: "purchases",
                columns: new[] { "ReceiptLineId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_StoreId_HouseholdId",
                table: "purchases",
                columns: new[] { "StoreId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_ProductId_HouseholdId",
                table: "receipt_lines",
                columns: new[] { "ProductId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_ReceiptId_HouseholdId",
                table: "receipt_lines",
                columns: new[] { "ReceiptId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_ReceiptId_Position",
                table: "receipt_lines",
                columns: new[] { "ReceiptId", "Position" },
                unique: true);
        }
    }
}
