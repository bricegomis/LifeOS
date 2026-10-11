using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FoodsAndReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM recipe_ingredients i JOIN recipes r ON r."Id"=i."RecipeId"
                    JOIN products p ON p.id=i."FoodItemId" WHERE p.household_id<>r."HouseholdId")
                  OR EXISTS (SELECT 1 FROM meal_food_lines l JOIN planned_meals m ON m."Id"=l."PlannedMealId"
                    JOIN day_plans d ON d."Id"=m."DayPlanId" JOIN weeks w ON w."Id"=d."WeekId"
                    JOIN products p ON p.id=l."FoodItemId" WHERE p.household_id<>w."HouseholdId")
                  OR EXISTS (SELECT 1 FROM article_price_entries e JOIN products p ON p.id=e."ArticleId"
                    JOIN stores s ON s."Id"=e."StoreId" WHERE p.household_id<>s."HouseholdId")
                  OR EXISTS (SELECT 1 FROM products p JOIN products o ON o.id=p.is_correction_of WHERE p.household_id<>o.household_id)
                  OR EXISTS (SELECT 1 FROM product_aliases a JOIN products p ON p.id=a."ProductId" WHERE p.household_id<>a."HouseholdId")
                  THEN RAISE EXCEPTION 'FoodsAndReceipts: reference ambiguë ou inter-foyers. Migration annulée.'; END IF;
                END $$;
                ALTER TABLE meal_food_lines DROP CONSTRAINT "FK_meal_food_lines_products_FoodItemId";
                ALTER TABLE recipe_ingredients DROP CONSTRAINT "FK_recipe_ingredients_products_FoodItemId";
                ALTER TABLE products DROP CONSTRAINT "FK_products_households_household_id";
                ALTER TABLE products ADD COLUMN "FoodItemId" uuid;
                ALTER TABLE products ADD COLUMN "PurchaseUnit" varchar(20) NOT NULL DEFAULT 'unit';
                ALTER TABLE products ADD COLUMN "Barcode" varchar(50);
                ALTER TABLE products ADD COLUMN "PiecesComparable" boolean NOT NULL DEFAULT false;
                UPDATE products SET "PurchaseUnit"=CASE WHEN NOT "PurchaseUnitConfirmed" THEN 'unknown'
                    WHEN "Unit"='Kilogram' THEN 'kg' WHEN "Unit"='Liter' THEN 'L' ELSE 'unit' END,
                                    "Barcode"=off_barcode;
                ALTER TABLE products ALTER COLUMN reference_unit SET DEFAULT '';
                ALTER TABLE products ALTER COLUMN source SET DEFAULT 'Manual';
                ALTER TABLE products ALTER COLUMN "Unit" SET DEFAULT 'Unit';
                ALTER INDEX idx_food_items_household_id RENAME TO "IX_products_household_id";
                """);

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PackageConfirmed",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageQuantity",
                table: "products",
                type: "numeric(16,6)",
                precision: 16,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageUnit",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "article_price_entries",
                type: "numeric(16,6)",
                precision: 16,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<bool>(
                name: "PackageConfirmed",
                table: "article_price_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageQuantity",
                table: "article_price_entries",
                type: "numeric(16,6)",
                precision: 16,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageUnit",
                table: "article_price_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PiecesComparable",
                table: "article_price_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptLineId",
                table: "article_price_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "article_price_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_stores_Id_HouseholdId",
                table: "stores",
                columns: new[] { "Id", "HouseholdId" });

            migrationBuilder.CreateTable(
                name: "foods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reference_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    calories_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    proteins_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    carbs_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    fats_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    Fodmap = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Nova = table.Column<int>(type: "integer", nullable: true),
                    DigestiveNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    off_barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    off_payload = table.Column<string>(type: "jsonb", nullable: true),
                    is_correction_of = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_foods", x => x.id);
                    table.UniqueConstraint("AK_foods_id_household_id", x => new { x.id, x.household_id });
                    table.ForeignKey(
                        name: "FK_foods_foods_is_correction_of",
                        column: x => x.is_correction_of,
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_foods_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawDocument = table.Column<string>(type: "text", nullable: false),
                    DocumentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StoreName = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    DeclaredLines = table.Column<int>(type: "integer", nullable: true),
                    DetectedLines = table.Column<int>(type: "integer", nullable: true),
                    Diagnostic = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.Id);
                    table.UniqueConstraint("AK_receipts_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_receipts_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipts_stores_StoreId_HouseholdId",
                        columns: x => new { x.StoreId, x.HouseholdId },
                        principalTable: "stores",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    RawSource = table.Column<string>(type: "text", nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    ExcludedMention = table.Column<bool>(type: "boolean", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    StatedUnitPrice = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: true),
                    SourceDiagnostic = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriceMismatchConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: false),
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
                name: "IX_products_FoodItemId_household_id",
                table: "products",
                columns: new[] { "FoodItemId", "household_id" });

            migrationBuilder.CreateIndex(
                name: "IX_article_price_entries_ReceiptLineId",
                table: "article_price_entries",
                column: "ReceiptLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_foods_household_id",
                table: "foods",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_foods_is_correction_of",
                table: "foods",
                column: "is_correction_of");

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

            migrationBuilder.CreateIndex(
                name: "IX_receipts_HouseholdId_DocumentHash",
                table: "receipts",
                columns: new[] { "HouseholdId", "DocumentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_StoreId_HouseholdId",
                table: "receipts",
                columns: new[] { "StoreId", "HouseholdId" });

            migrationBuilder.Sql("""
                CREATE TABLE food_product_mappings (
                  product_id uuid PRIMARY KEY REFERENCES products(id) ON DELETE RESTRICT,
                  food_id uuid NOT NULL REFERENCES foods(id) ON DELETE RESTRICT,
                  household_id uuid NOT NULL REFERENCES households("Id") ON DELETE RESTRICT,
                  evidence text NOT NULL
                );
                INSERT INTO foods (id, household_id, name, reference_unit, source, off_barcode, off_payload,
                  is_correction_of, created_at, updated_at, "IsArchived",
                  calories_per_unit, proteins_per_unit, carbs_per_unit, fats_per_unit)
                SELECT p.id, p.household_id, p.name, p.reference_unit, p.source, p.off_barcode, p.off_payload,
                  p.is_correction_of, p.created_at, p.updated_at, p."IsArchived",
                  p.calories_per_unit, p.proteins_per_unit, p.carbs_per_unit, p.fats_per_unit
                FROM products p WHERE p.reference_unit<>''
                  OR p.calories_per_unit IS NOT NULL OR p.proteins_per_unit IS NOT NULL OR p.carbs_per_unit IS NOT NULL OR p.fats_per_unit IS NOT NULL
                  OR EXISTS (SELECT 1 FROM recipe_ingredients i WHERE i."FoodItemId"=p.id)
                  OR EXISTS (SELECT 1 FROM meal_food_lines l WHERE l."FoodItemId"=p.id)
                  OR p.is_correction_of IS NOT NULL
                  OR EXISTS (SELECT 1 FROM products c WHERE c.is_correction_of=p.id);
                INSERT INTO food_product_mappings SELECT f.id, f.id, f.household_id, 'nutrition / usage alimentaire historique / correction' FROM foods f;
                UPDATE products p SET "FoodItemId"=m.food_id FROM food_product_mappings m WHERE m.product_id=p.id;
                UPDATE recipe_ingredients i SET "FoodItemId"=m.food_id FROM food_product_mappings m WHERE i."FoodItemId"=m.product_id;
                UPDATE meal_food_lines l SET "FoodItemId"=m.food_id FROM food_product_mappings m WHERE l."FoodItemId"=m.product_id;
                UPDATE article_price_entries e SET "Unit"=p."PurchaseUnit" FROM products p WHERE p.id=e."ArticleId";
                ALTER TABLE foods ADD CONSTRAINT foods_nova_check CHECK ("Nova" IS NULL OR "Nova" BETWEEN 1 AND 4);
                ALTER TABLE foods ADD CONSTRAINT foods_fodmap_check CHECK ("Fodmap" IS NULL OR "Fodmap" IN ('low','moderate','high'));
                ALTER TABLE article_price_entries ADD CONSTRAINT price_receipt_line_fk FOREIGN KEY ("ReceiptLineId") REFERENCES receipt_lines("Id") ON DELETE RESTRICT;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_meal_food_lines_foods_FoodItemId",
                table: "meal_food_lines",
                column: "FoodItemId",
                principalTable: "foods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_foods_FoodItemId_household_id",
                table: "products",
                columns: new[] { "FoodItemId", "household_id" },
                principalTable: "foods",
                principalColumns: new[] { "id", "household_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_households_household_id",
                table: "products",
                column: "household_id",
                principalTable: "households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_ingredients_foods_FoodItemId",
                table: "recipe_ingredients",
                column: "FoodItemId",
                principalTable: "foods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Restaurez une sauvegarde pré-migration. Aucun retour destructif automatique après les nouvelles écritures.");
    }
}
