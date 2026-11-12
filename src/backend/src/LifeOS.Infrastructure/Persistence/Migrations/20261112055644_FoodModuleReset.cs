using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FoodModuleReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Roadmap step 1 reset (ADR 0010, § 6): every table of the previous models lived in
            // "public" and is dropped with its data, including tables no longer mapped (stock,
            // shopping list, configurations). Only the migrations history is kept. On a new
            // database this block finds nothing to drop.
            migrationBuilder.Sql("""
                DO $$
                DECLARE legacy record;
                BEGIN
                    FOR legacy IN SELECT tablename FROM pg_tables
                        WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    LOOP
                        EXECUTE format('DROP TABLE IF EXISTS public.%I CASCADE', legacy.tablename);
                    END LOOP;
                END $$;
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" < '20261112055644_FoodModuleReset';
                """);

            migrationBuilder.EnsureSchema(
                name: "food");

            migrationBuilder.EnsureSchema(
                name: "households");

            migrationBuilder.EnsureSchema(
                name: "sports");

            migrationBuilder.CreateTable(
                name: "households",
                schema: "households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "foods",
                schema: "food",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reference_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    calories = table.Column<double>(type: "double precision", nullable: true),
                    proteins = table.Column<double>(type: "double precision", nullable: true),
                    carbs = table.Column<double>(type: "double precision", nullable: true),
                    fats = table.Column<double>(type: "double precision", nullable: true),
                    fibers = table.Column<double>(type: "double precision", nullable: true),
                    fodmap = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    nova = table.Column<int>(type: "integer", nullable: true),
                    digestive_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    image_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_foods", x => x.id);
                    table.UniqueConstraint("AK_foods_id_household_id", x => new { x.id, x.household_id });
                    table.CheckConstraint("ck_food_fodmap", "fodmap IS NULL OR fodmap IN ('low', 'moderate', 'high')");
                    table.CheckConstraint("ck_food_nova", "nova IS NULL OR nova BETWEEN 1 AND 4");
                    table.CheckConstraint("ck_food_reference_unit", "reference_unit IN ('', '100 g', '100 ml', '1 pièce')");
                    table.ForeignKey(
                        name: "FK_foods_households_household_id",
                        column: x => x.household_id,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_members",
                schema: "households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupabaseUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_members", x => x.Id);
                    table.UniqueConstraint("AK_household_members_SupabaseUserId_HouseholdId", x => new { x.SupabaseUserId, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_household_members_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_profiles",
                schema: "households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PortionCoefficient = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_profiles", x => x.Id);
                    table.UniqueConstraint("AK_member_profiles_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_member_profiles_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                schema: "food",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Servings = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.Id);
                    table.UniqueConstraint("AK_recipes_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_recipes_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sport_templates",
                schema: "sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sport = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DistanceKm = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Intensity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Calories = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sport_templates", x => x.Id);
                    table.CheckConstraint("ck_sport_duration", "\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= 1440");
                    table.CheckConstraint("ck_sport_values", "\"Calories\" >= 0 AND (\"DistanceKm\" IS NULL OR \"DistanceKm\" >= 0)");
                    table.ForeignKey(
                        name: "FK_sport_templates_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                schema: "food",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    IsOrganic = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.Id);
                    table.UniqueConstraint("AK_stores_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_stores_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "food",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    package_quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: false),
                    package_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.UniqueConstraint("AK_products_id_household_id", x => new { x.id, x.household_id });
                    table.CheckConstraint("ck_product_package_quantity", "package_quantity > 0");
                    table.CheckConstraint("ck_product_package_unit", "package_unit IN ('g', 'kg', 'ml', 'L', 'pièce')");
                    table.ForeignKey(
                        name: "FK_products_foods_food_id_household_id",
                        columns: x => new { x.food_id, x.household_id },
                        principalSchema: "food",
                        principalTable: "foods",
                        principalColumns: new[] { "id", "household_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_households_household_id",
                        column: x => x.household_id,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "food_reviews",
                schema: "food",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_reviews", x => x.Id);
                    table.UniqueConstraint("AK_food_reviews_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_food_review_content", "\"Score\" IS NOT NULL OR NULLIF(BTRIM(\"Comment\"), '') IS NOT NULL");
                    table.CheckConstraint("ck_food_review_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 10)");
                    table.ForeignKey(
                        name: "FK_food_reviews_foods_FoodItemId_HouseholdId",
                        columns: x => new { x.FoodItemId, x.HouseholdId },
                        principalSchema: "food",
                        principalTable: "foods",
                        principalColumns: new[] { "id", "household_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_food_reviews_household_members_CreatedByUserId_HouseholdId",
                        columns: x => new { x.CreatedByUserId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "household_members",
                        principalColumns: new[] { "SupabaseUserId", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_food_reviews_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredients",
                schema: "food",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_foods_FoodItemId",
                        column: x => x.FoodItemId,
                        principalSchema: "food",
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_ingredients_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "food",
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_reviews",
                schema: "food",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_reviews", x => x.Id);
                    table.UniqueConstraint("AK_recipe_reviews_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_recipe_review_content", "\"Score\" IS NOT NULL OR NULLIF(BTRIM(\"Comment\"), '') IS NOT NULL");
                    table.CheckConstraint("ck_recipe_review_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 10)");
                    table.ForeignKey(
                        name: "FK_recipe_reviews_household_members_CreatedByUserId_HouseholdId",
                        columns: x => new { x.CreatedByUserId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "household_members",
                        principalColumns: new[] { "SupabaseUserId", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_reviews_member_profiles_MemberProfileId_HouseholdId",
                        columns: x => new { x.MemberProfileId, x.HouseholdId },
                        principalSchema: "households",
                        principalTable: "member_profiles",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_reviews_recipes_RecipeId_HouseholdId",
                        columns: x => new { x.RecipeId, x.HouseholdId },
                        principalSchema: "food",
                        principalTable: "recipes",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                schema: "food",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.Id);
                    table.UniqueConstraint("AK_receipts_Id_HouseholdId", x => new { x.Id, x.HouseholdId });
                    table.CheckConstraint("ck_receipt_status", "\"Status\" IN ('pending', 'processed')");
                    table.ForeignKey(
                        name: "FK_receipts_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "households",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipts_stores_StoreId_HouseholdId",
                        columns: x => new { x.StoreId, x.HouseholdId },
                        principalSchema: "food",
                        principalTable: "stores",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_prices",
                schema: "food",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    observed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    package_quantity = table.Column<decimal>(type: "numeric(16,6)", precision: 16, scale: 6, nullable: false),
                    package_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_prices", x => x.id);
                    table.CheckConstraint("ck_product_price_package", "package_quantity > 0");
                    table.CheckConstraint("ck_product_price_positive", "price > 0");
                    table.ForeignKey(
                        name: "FK_product_prices_products_product_id_household_id",
                        columns: x => new { x.product_id, x.household_id },
                        principalSchema: "food",
                        principalTable: "products",
                        principalColumns: new[] { "id", "household_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_prices_stores_store_id_household_id",
                        columns: x => new { x.store_id, x.household_id },
                        principalSchema: "food",
                        principalTable: "stores",
                        principalColumns: new[] { "Id", "HouseholdId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_CreatedByUserId_HouseholdId",
                schema: "food",
                table: "food_reviews",
                columns: new[] { "CreatedByUserId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_FoodItemId_HouseholdId",
                schema: "food",
                table: "food_reviews",
                columns: new[] { "FoodItemId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_FoodItemId_MemberProfileId",
                schema: "food",
                table: "food_reviews",
                columns: new[] { "FoodItemId", "MemberProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_reviews_MemberProfileId_HouseholdId",
                schema: "food",
                table: "food_reviews",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_foods_household_id",
                schema: "food",
                table: "foods",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_household_members_HouseholdId",
                schema: "households",
                table: "household_members",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_household_members_SupabaseUserId",
                schema: "households",
                table: "household_members",
                column: "SupabaseUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_profiles_HouseholdId",
                schema: "households",
                table: "member_profiles",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_product_id_household_id",
                schema: "food",
                table: "product_prices",
                columns: new[] { "product_id", "household_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_product_id_store_id_observed_on",
                schema: "food",
                table: "product_prices",
                columns: new[] { "product_id", "store_id", "observed_on" });

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_store_id_household_id",
                schema: "food",
                table: "product_prices",
                columns: new[] { "store_id", "household_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_food_id_household_id",
                schema: "food",
                table: "products",
                columns: new[] { "food_id", "household_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_household_id_food_id",
                schema: "food",
                table: "products",
                columns: new[] { "household_id", "food_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipts_HouseholdId_DocumentHash",
                schema: "food",
                table: "receipts",
                columns: new[] { "HouseholdId", "DocumentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_StoreId_HouseholdId",
                schema: "food",
                table: "receipts",
                columns: new[] { "StoreId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_FoodItemId",
                schema: "food",
                table: "recipe_ingredients",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_ingredients_RecipeId",
                schema: "food",
                table: "recipe_ingredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_CreatedByUserId_HouseholdId",
                schema: "food",
                table: "recipe_reviews",
                columns: new[] { "CreatedByUserId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_MemberProfileId_HouseholdId",
                schema: "food",
                table: "recipe_reviews",
                columns: new[] { "MemberProfileId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_HouseholdId",
                schema: "food",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_reviews_RecipeId_MemberProfileId",
                schema: "food",
                table: "recipe_reviews",
                columns: new[] { "RecipeId", "MemberProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipes_HouseholdId",
                schema: "food",
                table: "recipes",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_sport_templates_HouseholdId",
                schema: "sports",
                table: "sport_templates",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_stores_HouseholdId",
                schema: "food",
                table: "stores",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        /// <summary>The reset cannot be undone: restore the backup taken before deploying instead.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_reviews",
                schema: "food");

            migrationBuilder.DropTable(
                name: "product_prices",
                schema: "food");

            migrationBuilder.DropTable(
                name: "receipts",
                schema: "food");

            migrationBuilder.DropTable(
                name: "recipe_ingredients",
                schema: "food");

            migrationBuilder.DropTable(
                name: "recipe_reviews",
                schema: "food");

            migrationBuilder.DropTable(
                name: "sport_templates",
                schema: "sports");

            migrationBuilder.DropTable(
                name: "products",
                schema: "food");

            migrationBuilder.DropTable(
                name: "stores",
                schema: "food");

            migrationBuilder.DropTable(
                name: "household_members",
                schema: "households");

            migrationBuilder.DropTable(
                name: "member_profiles",
                schema: "households");

            migrationBuilder.DropTable(
                name: "recipes",
                schema: "food");

            migrationBuilder.DropTable(
                name: "foods",
                schema: "food");

            migrationBuilder.DropTable(
                name: "households",
                schema: "households");
        }
    }
}
