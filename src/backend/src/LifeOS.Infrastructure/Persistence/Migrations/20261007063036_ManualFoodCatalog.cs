using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualFoodCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ArticleId",
                table: "food_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "food_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_food_items_ArticleId",
                table: "food_items",
                column: "ArticleId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_food_items_articles_ArticleId",
                table: "food_items",
                column: "ArticleId",
                principalTable: "articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_food_items_articles_ArticleId",
                table: "food_items");

            migrationBuilder.DropIndex(
                name: "IX_food_items_ArticleId",
                table: "food_items");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "ArticleId",
                table: "food_items");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "food_items");
        }
    }
}
