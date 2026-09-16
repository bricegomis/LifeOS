using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Jalon6_StockAndShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shopping_list_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroceryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityNeeded = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    QuantityFromStock = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Checked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_list_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shopping_list_items_articles_GroceryItemId",
                        column: x => x.GroceryItemId,
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shopping_list_items_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shopping_list_items_weeks_WeekId",
                        column: x => x.WeekId,
                        principalTable: "weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_items_articles_GroceryItemId",
                        column: x => x.GroceryItemId,
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_items_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_list_items_GroceryItemId",
                table: "shopping_list_items",
                column: "GroceryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_shopping_list_items_HouseholdId",
                table: "shopping_list_items",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_shopping_list_items_HouseholdId_WeekId",
                table: "shopping_list_items",
                columns: new[] { "HouseholdId", "WeekId" });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_list_items_WeekId",
                table: "shopping_list_items",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_GroceryItemId",
                table: "stock_items",
                column: "GroceryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_HouseholdId",
                table: "stock_items",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_HouseholdId_GroceryItemId",
                table: "stock_items",
                columns: new[] { "HouseholdId", "GroceryItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shopping_list_items");

            migrationBuilder.DropTable(
                name: "stock_items");
        }
    }
}
