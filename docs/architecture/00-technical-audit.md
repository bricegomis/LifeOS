# LifeOS — Audit technique (jalon 1)

Ce document consigne l'état factuel du dépôt au moment du premier jalon de la
roadmap technique (« Audit technique »). Il ne prescrit pas l'architecture
cible : voir `docs/architecture/01-target-architecture.md` et les ADR associés
pour les décisions.

## Organisation du dépôt

Le dépôt est un mono-repo :

- `src/frontend` : application Vue 3 + TypeScript.
- `src/backend` : API ASP.NET Core (`LifeOS.Api`), en Clean Architecture
  (`LifeOS.Domain`, `LifeOS.Application`, `LifeOS.Infrastructure`, `LifeOS.Api`).
- `supabase/migrations` : schéma SQL Supabase utilisé pour l'authentification
  et une partie de la persistance distante actuelle.

## Intégration continue

- `.github/workflows/build-push-docker-images.yml` construit et publie les
  images Docker de l'API et du frontend sur `main`.
- `.github/workflows/deploy-pages.yml` déploie le frontend sur GitHub Pages.
- Il n'existe aucune CI de tests automatisés (ni frontend, ni backend). La
  seule validation actuelle est `npm run build` et `npm run lint` côté
  frontend, et `dotnet build` côté backend.

## Frontend

### Stack

Vue 3.5, Vite 8, TypeScript 6, Pinia 3, PrimeVue 4, vue-router 5.

### Vues

`TodayView`, `WeeklyPlannerView`, `LibraryView`, `StoresView`, `ArticlesView`,
`SettingsView`, `LoginView`.

### Stores Pinia

`auth`, `weekContext`, `weekPlanner`, `planningRules`, `groceryStores`,
`groceryItems`.

### Données et génération

- `src/frontend/src/data/weekGenerator.ts` (~915 lignes) : moteur de
  génération de semaine, entièrement côté frontend.
- `src/frontend/src/data/localLibrary.ts` (~458 lignes) : bibliothèque
  alimentaire statique locale (composants, plats composés, activités).

### Persistance

- `localStorage` versionné avec des clés `lifeos.*.v1` (par exemple
  `lifeos.weekPlan.v1`, `lifeos.planningRules.v1`, `lifeos.context.v1`,
  `lifeos.groceryStores.v1`, `lifeos.groceryItems.v1`).
- Synchronisation Supabase optionnelle (activée seulement si les variables
  d'environnement `VITE_SUPABASE_URL` / `VITE_SUPABASE_ANON_KEY` sont
  définies).
- Les magasins et articles (`GroceryStore`, `GroceryItem`) sont aujourd'hui
  **local-only** : ils ne sont pas synchronisés vers Supabase ni portés par le
  backend en persistance réelle.

## Backend

L'API ASP.NET Core suit une organisation Domain / Application /
Infrastructure / Api par domaine fonctionnel :

- **Stores** : lecture seule (`GET /api/stores`).
- **Articles** : CRUD complet + historique de prix (`/api/articles`).
- **Library** : lecture seule sur seed statique (`/api/meal-components`,
  `/api/composite-dishes`, `/api/activities`).
- **Planning** : CRUD complet sur `PlanningRule` et `FrequencyRule`.
- **WeekContext** : `GET`/`PUT /api/week-context` par utilisateur.

Le `WeekPlan` (la génération de semaine elle-même) **n'est pas encore porté**
côté backend : la génération reste entièrement calculée dans le frontend
(`weekGenerator.ts`).

## Authentification

Le backend valide les JWT émis par Supabase via OIDC (validation de jeton,
pas de vérification locale de secret). Il n'existe pas de flux de connexion
propre à l'API : l'authentification est entièrement déléguée à Supabase Auth.

## Persistance backend

Toutes les répertoires d'infrastructure actuels (`InMemoryStoreRepository`,
équivalents pour articles, règles de planification, contexte de semaine) sont
**100 % en mémoire** : les données sont réamorcées (« reseed ») à chaque
redémarrage du processus. Il n'y a donc aujourd'hui aucune persistance
durable côté backend.

L'isolation actuelle des données se fait par `ownerId` / `sub` (l'identifiant
utilisateur Supabase), pas par foyer (« household »).

## Supabase

Une seule migration existe : `supabase/migrations/20260707_0001_lifeos_phase1.sql`.
Elle définit :

- `user_settings`
- `planning_rules`
- `week_context_config`
- `week_mode_overrides`
- `week_plans`

Ces tables sont clés et protégées par RLS sur `user_id`. Il n'existe **aucun**
schéma Supabase pour les recettes, composants, repas composés, stock, courses
ou nutrition : ces domaines n'ont jamais été portés en base relationnelle.

## Tests

Aucun test automatisé n'existe dans le dépôt (ni unitaire, ni d'intégration,
ni end-to-end). La validation actuelle repose uniquement sur `npm run build`
et `npm run lint` côté frontend.

## Fonctionnalités absentes de l'existant

Les éléments suivants ne sont pas implémentés dans le code actuel et devront
être construits :

- intégration Open Food Facts ;
- scan de codes-barres ;
- notion de foyer (household) et de membres ;
- portions par membre ;
- stock et liste de courses ;
- comparaison de prix entre magasins (au-delà de l'historique brut par
  article déjà présent).

## Ce qui doit être conservé

- Stack frontend : Vue, TypeScript, Pinia, PrimeVue, Vite.
- Layering backend en couches (Domain / Application / Infrastructure / Api).
- Authentification Supabase (JWT).
- Concepts fonctionnels articles et magasins (à faire évoluer, pas à
  réécrire depuis zéro).
- `WeekContext` et les règles de planification (`PlanningRule`,
  `FrequencyRule`) comme base conceptuelle.

## Ce qui doit fortement évoluer ou être reconstruit

- `weekGenerator.ts` : à adapter fortement pour s'appuyer sur un backend
  persistant et une notion de foyer.
- Le backend : passage d'un stockage in-memory à une persistance réelle
  (PostgreSQL), et introduction du foyer comme racine d'isolation.
- `localLibrary.ts` : à remplacer par une bibliothèque alimentaire persistée
  côté backend.
- Modèle de repas : recettes, composants, repas composés et portions par
  membre sont à reconstruire, ils n'existent pas dans le code actuel sous
  cette forme.
- Stock et liste de courses : à construire entièrement.
- Historique de prix / suggestions : à construire au-delà du stockage brut
  actuel.

## Risques et points d'attention

- Les données backend en mémoire actuelles ne sont **pas critiques** : elles
  peuvent être perdues sans impact, ce qui simplifie la migration.
- Les données `localStorage` ne sont utilisées qu'en import ponctuel côté
  utilisateur ; il n'y a pas de garantie de complétude côté frontend qui
  doive être préservée à tout prix.
- La migration de l'isolation actuelle par `user_id` vers une isolation par
  `household_id` est un changement structurant à traiter explicitement (voir
  `docs/architecture/decisions/0003-household-isolation.md`).
