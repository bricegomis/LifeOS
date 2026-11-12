# LifeOS — Architecture en place

Ce document décrit ce qui tourne aujourd'hui. La cible (modules, semainier, agent)
est dans l'[ADR 0009](decisions/0009-modular-monolith-and-planning-core.md).

```text
Angular 22 -- HTTPS / Bearer JWT --> ASP.NET Core -- EF Core / Npgsql --> PostgreSQL
     |                                  |
     +-------- Supabase Auth -----------+ (validation issuer/audience/signature)
```

- `src/frontend-angular` est le seul client, aussi empaqueté pour iPhone avec
  Capacitor. Il ne stocke aucune donnée métier et n'accède jamais à PostgreSQL.
- Supabase authentifie (Google OAuth, lien e-mail) et ne contient aucune donnée métier.
- `src/backend` résout l'identité du JWT vers un foyer, filtre chaque requête par
  ce foyer et persiste dans PostgreSQL. Des clés étrangères composites empêchent
  les liens entre foyers.
- Les photos de tickets et les images d'aliments sont sur des volumes durables,
  servies uniquement par des routes authentifiées.

## Décisions en vigueur

| ADR | Sujet |
| --- | --- |
| [0001](decisions/0001-postgresql-on-coolify.md) | PostgreSQL auto-hébergé comme base métier |
| [0002](decisions/0002-supabase-auth-only.md) | Supabase pour l'authentification uniquement |
| [0003](decisions/0003-household-isolation.md) | Isolation applicative par foyer |
| [0006](decisions/0006-foods-products-receipts.md) | Aliments, produits, tickets |
| [0007](decisions/0007-recipes-meal-lines-and-household-reviews.md) | Recettes et avis (les repas planifiés en sont retirés) |
| [0008](decisions/0008-remove-week-planner.md) | Retrait du premier semainier |
| [0009](decisions/0009-modular-monolith-and-planning-core.md) | Monolithe modulaire, semainier au cœur, agent IA externe |

Les ADR 0004 et 0005, entièrement remplacées, ont été supprimées ; elles restent
dans l'historique git.

## Configuration et livraison

- API : `Supabase:Url`, `Supabase:Audience`, `Cors:AllowedOrigins`, et
  `ConnectionStrings:Postgres` ou `DATABASE_URL`.
- Angular : `LIFEOS_API_URL`, `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`,
  écrits dans un `config.js` public. Clé anonyme uniquement.
- `/api/version` est public et indique le build de l'API.
- Routes Angular en hash ; base `/LifeOS/` pour GitHub Pages et `/` pour nginx.
- Les migrations s'appliquent au démarrage de l'API, sauf `SkipDatabaseMigration=true`.
  Sauvegarde PostgreSQL avant toute livraison avec migration ; retour arrière par
  restauration.
- Une fusion dans `main` publie les images Docker de l'API et d'Angular, que le
  serveur récupère seul. Un build ou une fusion ne prouve pas que la production
  fonctionne.
