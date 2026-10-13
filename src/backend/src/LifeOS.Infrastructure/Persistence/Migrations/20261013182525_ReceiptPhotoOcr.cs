using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptPhotoOcr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "receipts",
                type: "character varying(500)",
                maxLength: 500,
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
                name: "Status",
                table: "receipts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "draft");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "ImagePath",
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
                name: "Status",
                table: "receipts");
        }
    }
}
