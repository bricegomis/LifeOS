# LifeOS — Architecture en place

Ce document décrit ce qui tourne aujourd'hui. La cible (modules, semainier, agent)
est dans l'[ADR 0009](decisions/0009-modular-monolith-and-planning-core.md).

```text
Angular 22 ------ HTTPS / Bearer (jeton LifeOS) --> ASP.NET Core -- EF Core / Npgsql --> PostgreSQL
Claude (MCP) ---- HTTPS / Bearer (jeton MCP) ----->   |   |
garmin-sync ----- HTTPS / jeton d'import --------->   |   +-- volumes de fichiers
   |                                                  +-- Google (connexion uniquement)
   +-- Garmin Connect (seul le service l'appelle, jamais l'API)
```

- `src/frontend-angular` est le seul client, aussi empaqueté pour iPhone avec
  Capacitor. Il ne stocke aucune donnée métier et n'accède jamais à PostgreSQL.
- L'API est son propre serveur OAuth 2.1 (OpenIddict) : elle connecte avec Google,
  n'accepte que les adresses autorisées et émet les jetons de l'application
  ([guide](../google-oauth.md)).
- `src/backend` résout le compte du jeton vers un foyer, filtre chaque requête par
  ce foyer et persiste dans PostgreSQL. Des clés étrangères composites empêchent
  les liens entre foyers.
- Les photos de tickets, les images (aliments, recettes, exercices, tâches) et les
  fichiers de santé sont sur des volumes durables, servis uniquement par des routes
  authentifiées ; les fichiers de santé sont chiffrés au repos (`HealthFiles:EncryptionKey`).
- Modules (ADR 0009) : Foyer, Alimentation, Sport, Tâches, Santé et le module de synthèse
  Budget, chacun avec son schéma PostgreSQL. Santé et Budget sont privés au compte.
- L'endpoint `/mcp` expose les outils de l'assistant Claude (ADR 0011), avec les droits du
  compte connecté.
- `src/garmin-sync` est un conteneur à part qui lit Garmin Connect et écrit par
  `/api/import/garmin/*` avec un jeton d'import (ADR 0019).

## Décisions en vigueur

| ADR | Sujet |
| --- | --- |
| [0001](decisions/0001-postgresql-on-coolify.md) | PostgreSQL auto-hébergé comme base métier |
| [0003](decisions/0003-household-isolation.md) | Isolation applicative par foyer |
| [0006](decisions/0006-foods-products-receipts.md) | Aliments, produits, tickets |
| [0007](decisions/0007-recipes-meal-lines-and-household-reviews.md) | Recettes et avis (les repas planifiés en sont retirés) |
| [0008](decisions/0008-remove-week-planner.md) | Retrait du premier semainier |
| [0009](decisions/0009-modular-monolith-and-planning-core.md) | Monolithe modulaire, semainier au cœur, agent IA externe |
| [0010](decisions/0010-simplified-food-module.md) | Alimentation simplifiée |
| [0011](decisions/0011-conversational-assistant-mcp.md) | Assistant Claude (MCP), connexion et serveur OAuth propres (remplace 0002) |
| [0012](decisions/0012-food-classification.md) | Classement des aliments |
| [0013](decisions/0013-sport-module.md) | Module Sport : séances, objectifs, défis |
| [0014](decisions/0014-sport-daily-routines.md) | Sport : exercices et routines quotidiennes |
| [0015](decisions/0015-sport-pages-and-routine-sessions.md) | Sport : une page par élément, passages de routine |
| [0016](decisions/0016-angular-material-ui-kit.md) | Kit d'interface Angular Material 3 |
| [0017](decisions/0017-tasks-module.md) | Module Tâches |
| [0018](decisions/0018-health-module.md) | Module Santé, privé au compte |
| [0019](decisions/0019-garmin-import.md) | Import Garmin par un service externe |
| [0020](decisions/0020-budget-module.md) | Module Budget, privé au compte |

Les ADR 0004 et 0005, entièrement remplacées, ont été supprimées.

## Configuration et livraison

- API : section `Auth` (Google, adresses autorisées, clés des jetons, adresses de
  retour de l'application), `Cors:AllowedOrigins`, et `ConnectionStrings:Postgres` ou
  `DATABASE_URL`.
- Angular : `LIFEOS_API_URL` seul, écrit dans un `config.js` public.
- `/api/version` est public et indique le build de l'API.
- Routes Angular en hash ; base `/LifeOS/` pour GitHub Pages et `/` pour nginx.
- Les migrations s'appliquent au démarrage de l'API, sauf `SkipDatabaseMigration=true`.
  Sauvegarde PostgreSQL avant toute livraison avec migration ; retour arrière par
  restauration.
- Une fusion dans `main` publie les images Docker de l'API, d'Angular et de garmin-sync, que le
  serveur récupère seul. Un build ou une fusion ne prouve pas que la production
  fonctionne.
