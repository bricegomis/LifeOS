using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Persistence.Migrations;

public partial class UnifiedProducts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Exact existing links only; abort atomically on inconsistent tenant identities.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM food_items f JOIN articles a ON a."Id" = f."ArticleId"
                WHERE f.household_id <> a."HouseholdId"
              ) THEN
                RAISE EXCEPTION 'UnifiedProducts: association aliment/article inter-foyers. Resolution explicite requise.';
              END IF;
              IF EXISTS (
                SELECT 1 FROM articles a JOIN food_items f ON f.id = a."Id"
                WHERE f."ArticleId" IS DISTINCT FROM a."Id"
              ) THEN
                RAISE EXCEPTION 'UnifiedProducts: collision identifiants produit/article. Resolution explicite requise.';
              END IF;
            END $$;

            ALTER TABLE article_price_entries DROP CONSTRAINT "FK_article_price_entries_articles_ArticleId";
            ALTER TABLE food_items DROP CONSTRAINT "FK_food_items_articles_ArticleId";
            ALTER TABLE food_items DROP CONSTRAINT "FK_food_items_food_items_is_correction_of";
            ALTER TABLE food_items DROP CONSTRAINT "FK_food_items_households_household_id";
            ALTER TABLE meal_food_lines DROP CONSTRAINT "FK_meal_food_lines_food_items_FoodItemId";
            ALTER TABLE recipe_ingredients DROP CONSTRAINT "FK_recipe_ingredients_articles_FoodItemId";
            ALTER TABLE stock_items DROP CONSTRAINT "FK_stock_items_articles_GroceryItemId";
            ALTER TABLE shopping_list_items DROP CONSTRAINT "FK_shopping_list_items_articles_GroceryItemId";

            ALTER TABLE articles RENAME TO legacy_articles;
            ALTER TABLE food_items RENAME TO products;
            ALTER TABLE products RENAME CONSTRAINT "PK_food_items" TO "PK_products";
            DROP INDEX "IX_food_items_ArticleId";
            ALTER TABLE products ADD COLUMN "Description" varchar(1000) NOT NULL DEFAULT '';
            ALTER TABLE products ADD COLUMN "Unit" varchar(20) NOT NULL DEFAULT 'Unit';
            ALTER TABLE products ADD COLUMN "PurchaseUnitConfirmed" boolean NOT NULL DEFAULT false;
            ALTER TABLE products ADD COLUMN "LegacyPurchaseName" varchar(500);
            ALTER TABLE products ADD COLUMN "MigrationOrigin" varchar(20);
            ALTER TABLE products ADD CONSTRAINT "AK_products_id_household_id" UNIQUE (id, household_id);

            CREATE TABLE product_aliases (
                "ArticleId" uuid PRIMARY KEY, "ProductId" uuid NOT NULL, "HouseholdId" uuid NOT NULL,
                CONSTRAINT "FK_product_aliases_products_ProductId_HouseholdId"
                  FOREIGN KEY ("ProductId", "HouseholdId") REFERENCES products (id, household_id) ON DELETE RESTRICT
            );
            CREATE INDEX "IX_product_aliases_ProductId_HouseholdId" ON product_aliases ("ProductId", "HouseholdId");

            UPDATE products p
              SET "Description" = a."Description", "Unit" = a."Unit",
                  "PurchaseUnitConfirmed" = true, "MigrationOrigin" = 'linked',
                  "LegacyPurchaseName" = CASE WHEN p.name <> a."Name" THEN a."Name" ELSE NULL END
              FROM legacy_articles a WHERE p."ArticleId" = a."Id";
            UPDATE products SET "MigrationOrigin" = 'food-only' WHERE "ArticleId" IS NULL;

            INSERT INTO products (id, household_id, name, reference_unit, source, created_at, updated_at,
                                  "IsArchived", "Description", "Unit", "PurchaseUnitConfirmed", "MigrationOrigin")
              SELECT a."Id", a."HouseholdId", a."Name", '', 'Manual', a."CreatedAt", a."UpdatedAt",
                     false, a."Description", a."Unit", true, 'article-only'
              FROM legacy_articles a
              WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p."ArticleId" = a."Id");
            INSERT INTO product_aliases ("ArticleId", "ProductId", "HouseholdId")
              SELECT a."Id", COALESCE(p.id, a."Id"), a."HouseholdId"
              FROM legacy_articles a LEFT JOIN products p ON p."ArticleId" = a."Id";

            UPDATE recipe_ingredients r SET "FoodItemId" = a."ProductId"
              FROM product_aliases a WHERE r."FoodItemId" = a."ArticleId";
            UPDATE stock_items s SET "GroceryItemId" = a."ProductId"
              FROM product_aliases a WHERE s."GroceryItemId" = a."ArticleId";
            UPDATE shopping_list_items s SET "GroceryItemId" = a."ProductId"
              FROM product_aliases a WHERE s."GroceryItemId" = a."ArticleId";
            UPDATE article_price_entries e SET "ArticleId" = a."ProductId"
              FROM product_aliases a WHERE e."ArticleId" = a."ArticleId";

            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM recipe_ingredients i JOIN recipes r ON r."Id" = i."RecipeId"
                JOIN products p ON p.id = i."FoodItemId" WHERE p.household_id <> r."HouseholdId"
              ) OR EXISTS (
                SELECT 1 FROM stock_items s JOIN products p ON p.id = s."GroceryItemId"
                WHERE p.household_id <> s."HouseholdId"
              ) OR EXISTS (
                SELECT 1 FROM shopping_list_items s JOIN products p ON p.id = s."GroceryItemId"
                WHERE p.household_id <> s."HouseholdId"
              ) OR EXISTS (
                SELECT 1 FROM article_price_entries e JOIN products p ON p.id = e."ArticleId"
                JOIN stores s ON s."Id" = e."StoreId" WHERE p.household_id <> s."HouseholdId"
              ) OR EXISTS (
                SELECT 1 FROM products p JOIN products original ON original.id = p.is_correction_of
                WHERE p.household_id <> original.household_id
              ) OR EXISTS (
                SELECT 1 FROM meal_food_lines l JOIN planned_meals m ON m."Id" = l."PlannedMealId"
                JOIN day_plans d ON d."Id" = m."DayPlanId" JOIN weeks w ON w."Id" = d."WeekId"
                JOIN products p ON p.id = l."FoodItemId" WHERE p.household_id <> w."HouseholdId"
              ) THEN
                RAISE EXCEPTION 'UnifiedProducts: reference historique inter-foyers. Migration annulee sans perte.';
              END IF;
            END $$;

            ALTER TABLE products ADD CONSTRAINT "FK_products_households_household_id"
              FOREIGN KEY (household_id) REFERENCES households ("Id") ON DELETE CASCADE;
            ALTER TABLE products ADD CONSTRAINT "FK_products_products_is_correction_of"
              FOREIGN KEY (is_correction_of) REFERENCES products (id) ON DELETE SET NULL;
            ALTER TABLE meal_food_lines ADD CONSTRAINT "FK_meal_food_lines_products_FoodItemId"
              FOREIGN KEY ("FoodItemId") REFERENCES products (id) ON DELETE RESTRICT;
            ALTER TABLE recipe_ingredients ADD CONSTRAINT "FK_recipe_ingredients_products_FoodItemId"
              FOREIGN KEY ("FoodItemId") REFERENCES products (id) ON DELETE RESTRICT;
            ALTER TABLE stock_items ADD CONSTRAINT "FK_stock_items_products_GroceryItemId"
              FOREIGN KEY ("GroceryItemId") REFERENCES products (id) ON DELETE CASCADE;
            ALTER TABLE shopping_list_items ADD CONSTRAINT "FK_shopping_list_items_products_GroceryItemId"
              FOREIGN KEY ("GroceryItemId") REFERENCES products (id) ON DELETE CASCADE;
            ALTER TABLE article_price_entries ADD CONSTRAINT "FK_article_price_entries_products_ArticleId"
              FOREIGN KEY ("ArticleId") REFERENCES products (id) ON DELETE CASCADE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Un retour au modele double apres ecritures n'est pas automatique. Restaurer une sauvegarde pre-migration ou preparer une migration explicite.");
}
