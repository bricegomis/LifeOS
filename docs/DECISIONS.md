# LifeOS — Architectural and Product Decisions

This document captures the most important product and technical decisions that shape the current project. It is intentionally not a log of every implementation choice; it focuses on durable decisions that explain why the app currently looks and behaves the way it does.

## 2026-10-09 — Produit unique, nutrition et achats réunis

Décision explicite de l'utilisateur, pas un aliment générique associé à plusieurs
marques : un seul produit, une seule fiche et une seule création/édition.
Nutrition facultative (produits non alimentaires), unité d'achat indépendante,
historique de prix possédé, références canoniques pour recettes/stock/courses/repas.
Migration transactionnelle sans rapprochement par nom, alias historiques,
snapshots/prix/OFF/corrections/archivage et isolation foyer conservés.
Les anciens catalogues non associés restent distincts ; unité d'achat à confirmer.
Voir [ADR 0005](architecture/decisions/0005-unified-products.md) pour stratégie,
préconditions et limites de retour arrière. Les courses automatiques restent hors
MVP, y compris sur anciennes semaines ; aucune réécriture des listes historiques.

## 2026-10-07 — MVP semainier entièrement manuel (décision actuelle)

Le parcours livré est Angular + ASP.NET Core/EF Core/PostgreSQL.
Supabase assure l'authentification uniquement, avec JWT et isolation par foyer.
Vue/Pinia/localStorage est une interface historique, non concernée par la refonte.

Le MVP est un calendrier de sept jours (affichage 6–20, journée mobile) avec
plusieurs événements alimentation/sport, formulaires accessibles et trois
bibliothèques administrables. Recette ou produits directs ; portions personnelles
et préparation enfants séparées ; calories sportives manuelles, snapshots,
archivage et migration sans heures inventées. Voir le
[cadrage approuvé](01-scope-and-versions.md).

Les décisions ci-dessous sur Vue/local-first, trois repas fixes, génération,
alternance, contexte de travail et menu équilibré sont **historiques et remplacées
pour le parcours MVP**, pas une autorisation de supprimer leurs données.
Les décisions PostgreSQL/foyer/auth restent applicables. Les nouveaux événements
ne doivent pas être influencés silencieusement par les anciens réglages.

Documentation puis lots séquentiels autorisés sans approbation intermédiaire.
L'utilisateur a explicitement autorisé les PR/merges automatiques après contrôles,
sans attendre sa recette finale ; protections et CI restent obligatoires.
Pas de reset/suppression de données ni intervention manuelle de production.
Les workflows de livraison existants peuvent être déclenchés par les merges ;
un merge ne prouve pas un déploiement.

## 2026-10-07 — Contrôles Angular natifs, sans runtime PrimeNG

PrimeNG 22 et PrimeUI Themes 3 exigent une clé de licence et affichaient un
avis de licence invalide dans le navigateur. L'inspection a confirmé qu'aucun
composant/directive PrimeNG n'était utilisé : seulement le fournisseur Aura et
des classes sur les éléments HTML natifs.

Le coordinateur a approuvé leur retrait. Les contrôles natifs gardent les tokens
LifeOS, styles partagés, focus clavier et cibles tactiles ; les classes deviennent
`lifeos-button`/`lifeos-input`. PrimeIcons (MIT), réellement utilisé pour la
navigation, reste présent. Aucun mécanisme de licence n'est masqué ou contourné ;
les dépendances/runtime concernés sont retirés. Vue/PrimeVue historique inchangé.

## 2026 — Keep LifeOS focused on food and weekly planning
Context:
The project started as a general personal planning app idea and historically evolved from meal tracking toward a broader life dashboard. The current codebase deliberately narrows back to the nutrition-focused workflow.

Decision:
LifeOS remains a V0 weekly planner centered on meals, activity planning, and personal food organization.

Reasons:
- the application is more valuable when it solves one concrete problem well
- the meal-planning problem is clear and recurring
- the existing codebase already centers on weekly meal generation and context-aware planning

Consequences:
- the app does not expand into a generic life management system by default
- the architecture stays small and easy to evolve
- future features can be added later only if they directly support the food-planning workflow

## 2026 — Vue 3 + TypeScript + Pinia + PrimeVue
Context:
The project needs a fast, small UI with a clear state model and low operational overhead.

Decision:
The frontend uses Vue 3, TypeScript, Pinia, Vue Router, and PrimeVue.

Reasons:
- Vue provides a straightforward SPA model
- TypeScript helps keep the domain model explicit
- Pinia aligns well with a small but structured planner state model
- PrimeVue gives a quick, polished component system without adding a large bespoke design system

Consequences:
- the app stays maintainable and easy to evolve
- the domain model remains explicit in `src/types.ts`
- UI work stays close to the business domain rather than a large abstraction layer

## 2026 — Local-first persistence before backend dependency
Context:
The project originally had no backend and was designed as a practical personal app. The current architecture still reflects that mindset.

Decision:
The app stores key planner data in localStorage first, with optional Supabase sync for authenticated users.

Reasons:
- the main workflow should work without a backend
- a personal planner benefits from a simple local-first experience
- Supabase can be used later for synchronization across devices without forcing it on the core workflow

Consequences:
- the core app remains usable without external configuration
- Supabase is optional rather than mandatory
- local state and remote state must be normalized carefully to keep compatibility

## 2026 — Week-based planning as the primary unit
Context:
The project is built around the idea of reducing daily decision fatigue by planning once for the week.

Decision:
A `WeekPlan` is the primary object, and each day contains breakfast, lunch, dinner, and activity.

Reasons:
- the weekly rhythm is the user’s mental model
- repeated decisions are easier to manage when the week is generated as a whole
- the settings and rules naturally operate at the week / day level

Consequences:
- day-level and week-level contexts are treated as first-class concerns
- the generator flow focuses on planning a full 7-day cycle rather than isolated meals
- the UI is centered on “what is happening this week?” rather than event-by-event tracking

## 2026 — Rule-driven generation instead of rigid recipes
Context:
The app should help generate meal plans without forcing the user into a rigid, prescriptive system.

Decision:
The scheduler uses fixed rules and frequency rules to build a week rather than hard-coding a single meal matrix.

Reasons:
- the user has recurring preferences and exceptions
- the app should be semi-automatic, not a black box
- a rule model allows both structure and manual override

Consequences:
- `PlanningRule` handles “must happen this weekday / meal slot” logic
- `FrequencyRule` handles “this food should appear X times per week” logic
- the app can remain flexible while still reducing decision fatigue

## 2026 — Keep the data model narrow and practical
Context:
The repo historically considered broader life management and future features, but the current code remains a compact food-planning model.

Decision:
The core domain stays narrow: components, dishes, weekly plans, and week context.

Reasons:
- the system should remain easy to understand and maintain
- broad abstractions would not add value at this stage
- the project is personal and should evolve incrementally

Consequences:
- the domain model is explicit and readable rather than over-engineered
- future features can be added only when they clearly support the nutrition-planning use case

## 2026 — Optional Supabase auth and synchronization layer
Context:
The project already anticipates cross-device access but does not require a cloud backend for local use.

Decision:
Supabase is used for magic-link authentication and optional remote persistence of user settings and week data.

Reasons:
- it supports a simple personal multi-device workflow
- it remains optional and non-blocking for offline local use
- it fits the current app’s size and architecture

Consequences:
- localStorage remains the default storage model
- remote persistence is configured only when env vars are present
- future cross-device sync can evolve without rewriting the base app architecture

## 2026 — Google OAuth as primary Angular sign-in

The deployed Angular frontend uses the official Supabase SDK to redirect to
Google in the same tab, retaining email magic links as a fallback. This avoids
opening an email callback in another browser, particularly in Firefox private
windows, without replacing Supabase or changing the backend JWT provider.

The existing PKCE client, storage key, automatic renewal and auth-state
subscription remain. Angular explicitly exchanges callback codes and reports
refusal/exchange errors; post-login destinations are limited to internal routes.
Supabase owns verified same-email identity linking. No custom email-based user
merge, household migration, Microsoft provider or business-data migration is
introduced. Closing a private window still loses the PKCE verifier/session.

Google Cloud and Supabase provider setup, redirect allow-lists and real-browser
validation remain manual, documented in [the setup guide](google-oauth.md).

## 2026 — Grocery stores and articles as local, price-tracking building blocks
Context:
Meal planning benefits from knowing where groceries are bought and how their prices evolve, without turning the app into a full shopping or budgeting tool.

Decision:
Grocery stores (`GroceryStore`) and grocery articles (`GroceryItem`) are managed through dedicated CRUD views (`StoresView`, `ArticlesView`), each backed by its own local-first Pinia store. Each article keeps a reference unit (per kilogram, per liter, or per unit) and a price history (`GroceryPriceEntry`) of store + price + date observations.

Reasons:
- knowing "where" and "for how much" an article was bought is directly useful for the food-planning workflow, without requiring a shopping-list or budgeting feature
- keeping stores and articles as narrow, independent entities matches the existing CRUD pattern already used for stores
- price history as an append-only list per article is simple to reason about and does not require a backend

Consequences:
- `lifeos.groceryStores.v1` and `lifeos.groceryItems.v1` are added as new local-first `localStorage` keys, following the same schema-versioned persistence pattern as other stores
- these entities are not yet synced to Supabase; they remain local-only until a clear need for cross-device sync emerges
- shopping lists, budgeting, and automatic price analytics remain out of scope for now
- deleting a store does not cascade into existing price history entries; the UI tolerates orphaned `storeId` references and falls back to a "Magasin supprimé" label

## 2026 — Introduce a .NET backend API alongside the local-first frontend
Context:
The frontend has so far relied on localStorage plus optional Supabase sync, with no dedicated backend domain model. Some features (starting with grocery stores) call for server-side logic and a structured backend as the project grows.

Decision:
A new ASP.NET Core Web API (`src/backend`, `LifeOS.Api`) is introduced, organized into Domain, Application, Infrastructure, and Api layers pointing inward, as a foundation to grow towards Clean Architecture / DDD. The first implemented slice is a read-only `GET /api/stores` endpoint. The API authenticates requests using the same Supabase-issued JWTs already used by the frontend, instead of implementing a separate login flow.

Reasons:
- reusing Supabase Auth avoids duplicating identity management and keeps a single sign-in experience for the user
- a layered backend structure keeps business rules (Domain/Application) independent from framework and persistence details (Infrastructure/Api), making it easier to evolve incrementally
- starting with the smallest possible vertical slice (list stores) validates the wiring (auth, layering, DI) before adding write operations or a real database

Consequences:
- the backend does not replace the local-first frontend architecture; localStorage remains the default persistence for the core planning workflow
- stores are currently served from an in-memory, per-user seeded repository (`InMemoryStoreRepository`) rather than a real database; this is expected to change once the backend needs to persist writes
- future backend features should follow the same layering (Domain entities, Application use cases/ports, Infrastructure implementations, Api endpoint mapping) and reuse the existing Supabase JWT authentication

## 2026 — Expand the backend API to articles, meal library, planning rules, and week context; add Scalar for OpenAPI testing
Context:
The backend API started with a single read-only `GET /api/stores` slice. The frontend's domain model covers more entities (grocery articles, the shared meal library, planning/frequency rules, week context) that needed the same server-side treatment, and there was no interactive way to exercise the API without a separate tool.

Decision:
The backend now exposes, for each remaining bounded context, the same layering already used for stores (Domain aggregate mirroring the frontend model, Application query/command backed by a repository port, in-memory Infrastructure repository, Api endpoint mapping): full CRUD for grocery articles (`/api/articles`, including price history), read-only endpoints for the shared meal library (`/api/meal-components`, `/api/composite-dishes`, `/api/activities`), full CRUD for planning rules and frequency rules, and get/replace for the per-user week context (`/api/week-context`). `Scalar.AspNetCore` is added on top of the existing `Microsoft.AspNetCore.OpenApi` document to provide an interactive API reference/tester at `/scalar/v1` in development.

Reasons:
- following the same layering per bounded context keeps the backend consistent and easy to extend as more of the frontend domain is ported
- the meal library (components, dishes, activities) is shared, read-only seed data in the frontend, so it is exposed the same way rather than as per-owner CRUD
- Scalar gives a lightweight, native-OpenAPI-compatible UI to manually test authenticated endpoints without adding a heavier tool like Swashbuckle

Consequences:
- articles, planning rules, frequency rules, and week context are currently served from in-memory, per-owner repositories, not a real database; this is expected to change as the backend matures
- `WeekPlan` generation itself (the weekly planner, `src/frontend/src/data/weekGenerator.ts`) is not yet ported to the backend and still runs entirely in the frontend
- future backend work can keep using `/scalar/v1` to manually verify new endpoints against a real Supabase-issued JWT

## 2026 — Jalon 1 : fondations foyer et persistance PostgreSQL réelle (Stores, Articles)
Context:
La roadmap technique (`docs/03-roadmap.md`) et les ADR 0001-0003 avaient validé la cible (PostgreSQL auto-hébergé via EF Core, Supabase en auth uniquement, isolation applicative par foyer) sans encore d'implémentation concrète : le backend était entièrement en mémoire et scopé par utilisateur Supabase (`ownerId`), sans notion de foyer.

Decision:
Introduction du modèle `Household` / `HouseholdMember` (rôle `owner` au MVP, modèle prêt pour `member`) / `MemberProfile` dans `LifeOS.Domain`, et migration complète des domaines `Stores` et `Articles` (les plus proches d'être prêts selon l'audit) de l'in-memory vers PostgreSQL via EF Core (`LifeOSDbContext`, migrations versionnées). Chaque requête authentifiée résout le foyer de l'utilisateur via `ResolveHouseholdForUserQuery` : si l'utilisateur Supabase n'a pas encore de foyer (utilisateur pré-existant), un foyer propriétaire est provisionné à la demande plutôt que via une migration de données complexe. Toutes les tables migrées portent un `household_id` et sont filtrées systématiquement côté application (pas de RLS Postgres, la base applicative étant séparée de Supabase).

Reasons:
- Stores et Articles étaient les domaines les plus simples et les plus proches d'un modèle stable, permettant de prouver le socle (foyer + EF Core + Postgres + isolation) sans réattaquer en même temps le modèle repas/recettes plus complexe visé par le Jalon 2.
- Le provisionnement à la demande évite un script de migration de données pour un volume d'utilisateurs actuel faible et non critique (cf. audit technique), tout en restant compatible avec une évolution future vers des foyers multi-membres.
- L'isolation applicative (et non RLS) est cohérente avec l'ADR 0003 : la base PostgreSQL cible est un service distinct de Supabase, qui ne sert que l'authentification (ADR 0002).

Consequences:
- Les tables `stores`, `articles`, `article_price_entries` conservent leur forme héritée du modèle in-memory plutôt que d'adopter immédiatement le modèle conceptuel `food_items` / `price_observations` documenté dans `docs/architecture/02-data-model.md` ; cette divergence est documentée explicitement dans ce fichier et sera reconciliée au jalon « bibliothèque alimentaire ».
- Les contextes `Library`, `Planning` (règles) et `WeekContexts` restent en mémoire, scopés par utilisateur, jusqu'à leurs propres jalons de migration.
- Des tests automatisés (unitaires domaine + intégration API/EF Core via Testcontainers PostgreSQL) prouvent la persistance après redémarrage logique de l'API et l'isolation stricte entre deux foyers ; ils font désormais partie du socle de validation du backend (`dotnet test` depuis `src/backend`).

## 2026 — Menu équilibré unique : suppression du choix de scénario
Context:
La planification exposait trois scénarios concurrents que l'utilisateur devait choisir (`nutritional_balance`, `economy`, `reduce_waste`), via une case à cocher par objectif dans la page Planning. Le `DeterministicScenarioEngine` ne calculait rien : il renvoyait des deltas codés en dur par objectif. L'utilisateur a demandé explicitement à ne plus avoir à choisir, et à obtenir un menu cherchant l'équilibre nutritionnel, un budget minimum, de la diversité sur le mois et l'absence de gaspillage.

Decision:
Un seul calcul, `BalancedPlanEngine`, remplace les scénarios. Il évalue simultanément quatre dimensions à partir des données réelles du foyer (repas planifiés, recettes, prix observés, stock, cibles nutritionnelles, activités) : nutrition (poids 0,40), coût (0,25), anti-gaspillage (0,20) et diversité sur ~35 jours (0,15). La nutrition est traitée comme une **contrainte** : sous un score de 0,60 le score global est plafonné à celui de la nutrition et l'arbitrage est affiché. Les dimensions non calculables (données manquantes) sont exclues et les poids restants sont renormalisés, plutôt que de produire un score inventé. L'entité `WeekScenario` devient `BalancedWeekPlan`, l'API devient `/api/weeks/{weekId}/balanced-plan` (`GET`, `POST /compute` sans corps, `PATCH /{planId}/apply`), et l'UI Angular propose un unique bouton « Calculer le menu équilibré » suivi du détail des scores, des arbitrages et des limites.

Reasons:
- l'utilisateur ne doit plus arbitrer lui-même entre des objectifs qui, en pratique, doivent être tenus ensemble ;
- la nutrition est une contrainte de santé, pas un critère échangeable contre du budget : le plafonnement du score le rend explicite ;
- exclure une dimension non calculable et le dire est plus honnête que de la scorer par défaut ;
- réutiliser les modèles existants (recettes, articles et historique de prix, stock, configuration nutritionnelle) évite d'inventer un nouveau modèle de données.

Consequences:
- La table `week_scenarios` et la colonne `RankingObjective` sont **conservées telles quelles** (la colonne porte désormais la valeur unique `balanced`), donc aucune migration n'est nécessaire.
- Le score anti-gaspillage est un **proxy assumé** (couverture par le stock + réemploi des ingrédients entre repas) : le modèle ne connaît ni dates de péremption ni tailles de conditionnement, donc aucune promesse de « zéro gaspillage » n'est faite — la limite est affichée dans l'UI à chaque calcul.
- Le coût est normalisé par rapport à l'amplitude réellement atteignable avec la bibliothèque de recettes du foyer ; en dessous de deux recettes entièrement valorisables, la dimension est déclarée non calculable.
- Les conversions d'unités ne sont pas gérées (unité de recette vs unité d'article) et les quantités sont agrégées telles quelles ; c'est une limite connue du calcul de coût et de nutrition.
- Une semaine vide ne reçoit pas de score global : l'API renvoie `overallScore: null` avec une limite explicite.

## 2026 — Créations et modifications dans un dialogue partagé
Context:
L'utilisateur trouvait l'interface trop complexe : chaque page exposait en permanence un formulaire (panneau collant, éditeur inséré au-dessus du calendrier, formulaires en ligne), ce qui mélangeait consultation et saisie.

Decision:
Le frontend Angular utilise un composant unique `app-lifeos-dialog` (élément natif `<dialog>` ouvert en modal, sans nouvelle dépendance) pour toutes les créations et modifications. Les pages ne gardent qu'un bouton de création clair et leurs listes/calendrier. Le même dialogue sert à créer et à modifier un élément. Les champs secondaires sont repliés dans « Plus d'options » et ouverts automatiquement s'ils contiennent déjà une valeur. La logique de fermeture (ignorer pendant l'enregistrement, confirmer si la saisie a changé) est un module pur testé (`dialog-guard.ts`) ; la validation des événements du calendrier est extraite dans `event-draft.ts` et testée.

Reasons:
- séparer consultation et saisie rend chaque page plus lisible, surtout sur mobile ;
- un seul composant garantit un comportement accessible homogène (titre, focus, Échap, erreurs, état d'attente) ;
- `<dialog>` natif suffit et évite d'ajouter une bibliothèque UI.

Consequences:
- Les payloads et routes API sont inchangés ; aucune fonctionnalité n'est retirée.
- Un dialogue ne se ferme qu'après succès ; une erreur reste affichée dans le dialogue avec la saisie conservée.
- `tsconfig.json` autorise les imports relatifs en `.ts` pour que les modules purs restent testables avec `node --test`.
