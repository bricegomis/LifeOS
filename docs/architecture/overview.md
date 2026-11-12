# LifeOS — Architecture en place

Ce document décrit ce qui tourne aujourd'hui. La cible (modules, semainier, agent)
est dans l'[ADR 0009](decisions/0009-modular-monolith-and-planning-core.md).

```text
Angular 22 -- HTTPS / Bearer (jeton LifeOS) --> ASP.NET Core -- EF Core / Npgsql --> PostgreSQL
                                                 |
                                                 +-- Google (connexion uniquement)
```

- `src/frontend-angular` est le seul client, aussi empaqueté pour iPhone avec
  Capacitor. Il ne stocke aucune donnée métier et n'accède jamais à PostgreSQL.
- L'API est son propre serveur OAuth 2.1 (OpenIddict) : elle connecte avec Google,
  n'accepte que les adresses autorisées et émet les jetons de l'application
  ([guide](../google-oauth.md)).
- `src/backend` résout le compte du jeton vers un foyer, filtre chaque requête par
  ce foyer et persiste dans PostgreSQL. Des clés étrangères composites empêchent
  les liens entre foyers.
- Les photos de tickets et les images d'aliments sont sur des volumes durables,
  servies uniquement par des routes authentifiées.

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

Les ADR 0004 et 0005, entièrement remplacées, ont été supprimées ; elles restent
dans l'historique git.

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
- Une fusion dans `main` publie les images Docker de l'API et d'Angular, que le
  serveur récupère seul. Un build ou une fusion ne prouve pas que la production
  fonctionne.
