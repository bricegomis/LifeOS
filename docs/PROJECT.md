# LifeOS — Project Memory

## État actuel — aliments et tickets manuels

Angular 22 (`src/frontend-angular`) est le seul frontend. API ASP.NET Core /
EF Core / PostgreSQL (`src/backend`) source de vérité, Supabase auth/JWT seulement,
isolation par foyer. Identité visuelle et dialogues natifs LifeOS conservés.

Aliments génériques au premier plan : base/macros/FODMAP/NOVA manuels et nullable,
notes facultatives, filtre À compléter, zéro valide. Produits commerciaux secondaires,
plusieurs par aliment, contenance confirmée et prix observés/provenance. Recettes et
nouvelles lignes de repas sur aliments, pas de marque choisie implicitement.
Les fiches aliments s'ouvrent dans une popup centrée, avec marges et défilement
interne sur mobile comme sur ordinateur. L'édition revient à la fiche et conserve
les avis en cours ; fermer une saisie modifiée demande confirmation.
La liste des aliments utilise des cartes entièrement cliquables et accessibles au
clavier, sans bouton Ouvrir ni action d'archivage. L'archivage reste dans la fiche.
Les cartes affichent la moyenne des avis notés du foyer sur 10 (zéro compris) et
leur nombre ; les avis uniquement commentés n'inventent aucune note. Les filtres
et les prix observés avec leur provenance restent disponibles.
Les avis existants restent dans les fiches et sélecteurs. Ajouter ou modifier un
avis ouvre une popup centrée avec marges, au-dessus de la fiche éventuelle :
annulation/enregistrement revient au déclencheur, sans fermer la fiche ni son
brouillon. Une saisie modifiée demande confirmation ; une requête en cours
empêche la fermeture et les erreurs restent visibles.

Tickets photo ou JSON fichier/texte : source brute et toutes lignes avant validation,
contexte magasin/date explicite, pending/validated/ignored, reprise API et progression.
JSON : validation individuelle transactionnelle aliment/produit/achat/prix/statut.
Photo : Tesseract local (fra+eng), propositions OCR françaises corrigées manuellement,
validation atomique du ticket complet. Photos originales et texte OCR restent en
brouillon; les fichiers résident sur un volume durable sauvegardé avec PostgreSQL.
Concurrence/idempotence, réimport identique détecté par foyer, aucune santé importée.
Minimum des derniers relevés par produit/magasin uniquement par base comparable,
historique et informatif, pas un produit de recette.

`FoodsAndReceipts` remplace le modèle unifié : produits/IDs/prix/alias/sources
conservés, mapping alimentaire explicite sans homonymes fusionnés, références
rebranchées et snapshots/horaires/portions inchangés. Anciens produits sans preuve
alimentaire restent non liés. Stock/courses et catalogues alimentaires parallèles
retirés des parcours/API/DI ; tables historiques non mappées. Vue et OFF actifs supprimés.
Voir [ADR 0006](architecture/decisions/0006-foods-products-receipts.md).

### Modèle actif — recettes, repas et avis

[ADR 0007](architecture/decisions/0007-recipes-meal-lines-and-household-reviews.md)
remplace le modèle culinaire à repas composés. La bibliothèque contient des
recettes de toute taille ; chaque repas manuel peut combiner plusieurs recettes
et des préparations ponctuelles persistées. Les portions et leur répartition
sont propres à chaque ligne. Des avis privés au foyer portent séparément sur
les aliments, recettes et groupes de profils. Les écrans Angular et l'API
utilisent le modèle aliments/recettes/lignes de repas, sans conversion ni
compatibilité avec les anciens payloads culinaires.

Cette cible ne change pas le modèle générique aliments/produits/tickets de l'ADR
0006. La migration `FlexibleRecipeMealLinesAndReviews` supprime les anciennes
recettes, lignes de repas, repas composés et règles/scénarios concernés, tout en
préservant les comptes, foyers, profils, aliments, produits, magasins, prix et
données sportives. Elle ne modifie aucune migration historique. Elle n'a pas
été appliquée à une base de production ; son application à un environnement
existant nécessite sauvegarde et autorisation explicite.

Le semainier est manuel : création de semaines vides, repas multi-lignes,
préparations ponctuelles, portions fractionnaires par ligne, duplication et
nutrition calculée avec inconnues explicites. Les quantités requises sont
affichées par ingrédient dans le détail du repas. Aucun agrégateur ni parcours
actif de liste de courses, aucune règle ni génération automatique de semaine
n'est actuellement exposé ; les anciens scénarios ne sont pas convertis.

Le sport, les profils existants, les foyers, l'authentification, les aliments,
produits, magasins, prix et tickets restent hors du nettoyage. Supabase conserve
uniquement l'authentification ; l'API/PostgreSQL est la source métier. Une
bibliothèque ou semaine vide reste vide et utile, sans fallback de démonstration.

Les tests utilisent PostgreSQL éphémère et couvrent le nouveau contrat, les
références croisées de foyer et la migration ; les contrôles Angular couvrent
type-check, tests, lint et build. Ne pas lancer ces migrations sur une base
partagée sans sauvegarde et autorisation explicite.

**Le texte ci-dessous conserve la mémoire historique** de Vue/local-first et
des parcours de génération ; il ne décrit plus la cible active. Les décisions
de génération/alternance/menu équilibré sont retirées du produit actif. Les
anciennes données culinaires/planning concernées sont supprimées par la migration
EF décrite ci-dessus ; seul ce texte historique reste dans la documentation.
Les détails d'authentification et d'isolation restent valides.

LifeOS is a personal planning app built to reduce daily decision fatigue around food and weekly activity. The current implementation is intentionally not a broad “all-of-life” dashboard. It is a narrow, practical tool focused on meals, planned eating, and the weekly rhythm that supports them.

## Why this project exists
The project addresses a recurring personal problem: deciding what to eat, what to prepare, and what physical activity to schedule each day is repetitive and cognitively expensive. The app aims to give the user a clear weekly view so that meals and activity can be planned once and then followed with less friction.

## Current scope
The active product scope is the V0 Weekly Planner:
- daily meal overview (`Today`)
- weekly planner view
- food library for components and dishes
- settings for context and planning rules
- generation of a full week from rules and constraints

This is not a generic personal life operating system. The focus remains on nutrition and planning, with room to evolve only when the core food-planning workflow is stable.

## Implemented features
The current codebase already includes the core of the weekly meal planner:

### Daily view
The `TodayView` resolves the current date against the current `WeekPlan` and shows:
- breakfast, lunch, and dinner cards
- estimated calories and protein totals for the day
- the scheduled activity
- work-location context (home / office / off) and bike commute status

### Weekly planner
The `WeeklyPlannerView` handles a 7-day week grid with:
- one row per day
- breakfast, lunch, dinner, and activity columns
- generation or regeneration of the week from configured rules
- week navigation by offset
- manual meal editing via a side drawer
- activity selection per day

### Library
The `LibraryView` exposes the local meal catalog:
- active meal components grouped by type (protein, starch, vegetable, optional)
- composite dishes
- physical activities
- search by name

### Grocery stores
The `StoresView` provides a CRUD interface for grocery stores (`GroceryStore`): name, optional address, search, create, edit, and delete. Data is persisted locally via the `groceryStores` Pinia store.

### Articles and price history
The `ArticlesView` provides a CRUD interface for grocery articles (`GroceryItem`): name, reference unit (per kilogram, per liter, or per unit), search, create, edit, and delete. Each article also keeps a price history (`GroceryPriceEntry`) of purchases: price, grocery store, and purchase date, so the same article can be compared across stores and over time. Data is persisted locally via the `groceryItems` Pinia store.

### Settings and generation rules
The `SettingsView` lets the user configure:
- the reference week and alternation mode
- per-week override exceptions
- separate day-by-day work context and bike commute templates for weeks with and without a child
- fixed meal rules (`PlanningRule`) and weekly frequency rules (`FrequencyRule`) that apply to all weeks or one type

### Rule-driven generation
The generator in `src/frontend/src/data/weekGenerator.ts` builds a full `WeekPlan` from:
- active meal components and dishes
- fixed rules
- frequency rules
- week context and week mode

The default rule set already encodes a practical weekly rhythm for the current user, including recurring lunch / dinner preferences and enjoyment-focused dishes.

## Architecture
The frontend is centered on a Vue 3 / TypeScript app in `src/frontend`. A backend API is
being introduced incrementally in `src/backend` (see "Backend API" below); it does not
replace the local-first frontend architecture described in this document.

### Main folders
- `src/frontend/src/views` contains the UI pages.
- `src/frontend/src/stores` contains the Pinia state management layer.
- `src/frontend/src/data` contains the static library and generator logic.
- `src/frontend/src/services/supabase` contains the optional remote sync layer.
- `src/frontend/src/types.ts` defines the domain model.
- `supabase/migrations` contains the database schema used for authenticated user data.

### Representative domain model
Key types in `src/types.ts`:
- `MealComponent`: reusable ingredient with nutrition data and category
- `CompositeDish`: complete dish treated as a single unit
- `MealSlot`: a planned meal with type and definition
- `DayPlan`: breakfast, lunch, dinner, and activity for a day
- `WeekPlan`: a full week with 7 days and status
- `WeekContext`: alternating week config, overrides, and per-day context
- `PlanningRule`: fixed meal assignment by weekday + meal type
- `FrequencyRule`: target count per week for a component, dish, or category
- `GroceryStore`: a grocery store with a name and optional address
- `GroceryItem`: a purchasable grocery article with a reference unit (kilogram, liter, or unit) and a price history
- `GroceryPriceEntry`: a single observed price for a `GroceryItem` at a given `GroceryStore` and date

## Data flow and planning logic
The planning engine follows a simple pattern:
1. resolve the current week mode (`kids` vs `solo`) from the reference configuration and overrides, unless the user chooses a one-week override in the planner
2. create a 7-day empty week
3. apply shared and matching type-specific fixed rules
4. apply shared and matching type-specific frequency rules to fill remaining slots
5. complete remaining meals from available library entries
6. generate the activity plan for each day
7. persist the resulting `WeekPlan`

The `weekContext` store carries the alternating week mode, per-week overrides, and separate weekday context templates for both week types. Choosing a type for a generated week snapshots that type’s day contexts and scopes its meal rules without changing the household’s default alternation.

Existing planning and frequency rules with no week type remain shared across both types. This preserves current plans and settings when upgrading; users can add new rules scoped to “Avec enfant” or “Sans enfant” for different meal patterns. For a fixed meal slot, a type-specific rule takes precedence over its shared rule. For a frequency target, a type-specific objective replaces the shared objective for that same target, while unrelated shared objectives continue to apply.

## Persistence model
The app currently uses a dual persistence strategy:

### Local-first persistence
The stores persist their state in `window.localStorage`:
- `lifeos.weekPlan.v1`
- `lifeos.planningRules.v1`
- `lifeos.context.v1`
- `lifeos.groceryStores.v1`
- `lifeos.groceryItems.v1`

This keeps the app usable offline and prevents the project from depending on a backend for the core workflow.

### Optional Supabase sync
The app also includes a Supabase integration for authentication and remote persistence when environment variables are configured:
- `VITE_SUPABASE_URL`
- `VITE_SUPABASE_ANON_KEY`

The repository layer (`lifeosRepository.ts`) syncs:
- user settings
- planning rules
- week context config
- week mode overrides
- week plans

The deployed Angular frontend uses Google OAuth through Supabase Auth as its
primary sign-in method, with magic-link email sign-in as a fallback. The legacy
Vue frontend retains email sign-in. Angular keeps the existing browser session
storage key, automatic token renewal, Supabase JWTs and household isolation.
Provider activation and real Google/private-Firefox verification are manual;
see [Google OAuth setup and validation](google-oauth.md).

Magic-link callback handling (Vue `services/supabase/magicLinkCallback.ts`, Angular `core/auth/magic-link-callback.ts`):
- the default PKCE link (`?code=...#/login?...`) only works in the browser that requested it, because the code verifier lives in that browser's storage; otherwise the login page now explains why instead of silently asking for the email again
- Supabase error redirects (e.g. `otp_expired`, often caused by an already-used or pre-fetched link) replace the hash route; the app restores `#/login` and shows the error
- `token_hash` links are also accepted (verified client-side with `verifyOtp`), which works across browsers/devices. This requires changing the Supabase "Magic Link" email template (out of repo), e.g. `<a href="{{ .SiteURL }}/?token_hash={{ .TokenHash }}&type=email#/login">` (Site URL = deployed app URL)
- Supabase dashboard (out of repo): the app URL must be the Site URL or be listed in Auth → URL Configuration → Redirect URLs (e.g. `https://<host>/**`), otherwise Supabase redirects to the Site URL instead

Angular also handles Google callbacks using the same PKCE client: codes are
captured and removed before routing, then explicitly exchanged once so callback
errors cannot be hidden by SDK initialization. Only known internal routes are
accepted as post-login destinations. Same-address verified identity linking is
delegated to Supabase; LifeOS does not merge users or households by email.

## Backend API
`src/backend` hosts a growing ASP.NET Core Web API (`LifeOS.Api`) for LifeOS, organized
into Domain / Application / Infrastructure / Api layers pointing inward (Clean
Architecture), as a foundation to grow towards a DDD-oriented backend.

It now covers most of the frontend's core domain, each following the same per-bounded-
context layering: read-only `GET /api/stores` (grocery stores); full CRUD
`/api/articles` (grocery articles + price history); read-only `/api/meal-components`,
`/api/composite-dishes`, `/api/activities` (the shared meal library, still in-memory);
full CRUD `/api/planning-rules`, `/api/frequency-rules`, `/api/recipes`,
`/api/composed-meals`, `/api/food-items`, `/api/stock-items`, and the shopping list;
`GET`/`PUT /api/week-context` (per-household week planning context, including both day-context
templates); and week
planning (`/api/weeks` stores the selected or alternation-derived type and initializes its days
from that type's template, `/api/day-plans`, `/api/planned-meals`,
`/api/weeks/{weekId}/balanced-plan`) including the single balanced-plan computation on the
API side. All of these are
persisted in PostgreSQL, scoped by household — see `src/backend/README.md` for the
full endpoint/persistence table. The frontend's own `WeekPlan` generator
(`src/frontend/src/data/weekGenerator.ts`) still runs client-side and has not yet been
unified with the backend's balanced-plan engine. All endpoints reuse the frontend's existing
Supabase Auth session: the API validates the same Supabase-issued JWT access tokens
rather than implementing its own login flow (Supabase remains auth-only, see ADR 0002).

As of Milestone 1 ("fondations foyer"), the API has a `Household` /
`HouseholdMember` / `MemberProfile` domain model backed by real PostgreSQL
persistence via EF Core, and every household-scoped bounded context (Stores,
Articles, Recipes, Composed Meals, Week Planning, Food Items, Nutrition/Activity
Sessions, Planning/Frequency Rules, Week Context, Stock, Shopping List) has since
been migrated from in-memory `ConcurrentDictionary` repositories to this database,
scoped by `household_id` rather than by raw Supabase user id.
Isolation between households is enforced at the application layer (ADR 0003):
each authenticated request resolves the caller's household on demand — a
household is auto-provisioned the first time a Supabase user is seen post-
migration — and every query/command for a persisted domain is filtered by that
household id; there is no Postgres RLS, since the application database is
separate from Supabase (Supabase is auth-only). Only the shared meal library
(meal components, composite dishes, activities) is still in-memory and
read-only, pending a future migration to a persisted, queryable catalog. The
API also exposes an OpenAPI document and an interactive Scalar UI (`/scalar/v1`
in development) to test it without a separate tool. See `src/backend/README.md`
for the layer breakdown, endpoint list, and configuration details.

## Current state of the project
At the code level, the project is already working as a pragmatic V0 product:
- the app is structured around the weekly meal planning domain
- library, settings, and week generation are implemented
- a daily “today” overview is present
- local persistence is in place
- Supabase support exists but is optional and configured by environment
- no broad generic LifeOS features are implemented yet

The current state is intentionally narrow and product-focused on nutrition and planning rather than a full personal operating system.

## Known limitations
- The app is still centered on a single user / personal workflow.
- The backend (`LifeOS.Api`) now covers stores, articles, the shared meal library, planning/frequency rules, week context, recipes, composed meals, food items, week planning, and stock/shopping list. All of these except the shared meal library (meal components, composite dishes, activities, still in-memory) are persisted in PostgreSQL via EF Core, scoped by household. The frontend's `WeekPlan` generator still runs client-side and is not yet unified with the backend's own balanced week-plan engine.
- The meal library is a curated local catalog, not a general recipe database.
- The project now has an automated backend test suite (`src/backend/tests`): domain unit tests and API/EF Core integration tests (Testcontainers-backed PostgreSQL) proving persistence across process restarts and strict cross-household isolation. The frontend still has no dedicated automated test suite; its main quality gate remains the build and lint flow.
- The app is still at the V0 planning stage; advanced nutrition tracking, shopping lists, and broader life-management features are out of scope.

## Current State
The repository currently reflects a functioning V0 weekly planner for food-oriented planning. The core product is stable enough to be used as a focused nutrition planning app, with the main architecture centered on local-first state plus optional Supabase sync.

## Next Steps
The logical next step is not a broad product expansion. It is to keep the system focused and reliable by:
1. validating the current weekly generation flow against real-world usage
2. strengthening the meal library and rule quality over time
3. refining the planner UX around meal editing and week adjustments
4. adding only the missing nutrition-tracking features that are clearly part of the food-planning workflow
5. keeping the docs aligned with code as the product evolves
