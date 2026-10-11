# LifeOS — Architecture active

```text
Angular 22 -- HTTPS / Bearer JWT --> ASP.NET Core -- EF Core / Npgsql --> PostgreSQL
     |                                  |
     +-------- Supabase Auth -----------+ (validation issuer/audience/signature)
```

`src/frontend-angular` est le seul client, avec contrôles natifs, dialogues et
tokens LifeOS. Supabase authentifie (Google OAuth / magic link), sans données métier.
`src/backend` résout l'identité JWT vers un foyer, filtre chaque requête et persiste
dans PostgreSQL. FK composites renforcent les liens inter-foyers des nouveaux domaines.
Le navigateur n'est pas une source de vérité métier et n'accède pas directement à PostgreSQL.

## Domaines et parcours

`FoodItem` est l'aliment générique, `Product` sa référence commerciale secondaire.
Recettes/repas utilisent les aliments, nutrition manuelle nullable et snapshots
historiques précis. Tickets : source brute, lignes persistées, choix manuel,
transaction aliment/produit/achat/prix/statut, verrou de document et contraintes
uniques. Prix observés comparés par base sûre, jamais optimisation de marque.
Voir [ADR 0006](decisions/0006-foods-products-receipts.md) et [modèle](02-data-model.md).

Stock, courses, catalogues alimentaires en mémoire parallèles et OFF actifs retirés.
Planning/contextes/règles historiques, auth et sport sont conservés ; le MVP manuel
n'applique pas ces règles aux nouvelles semaines. Le calcul équilibré historique
ne dépend plus du stock, expose une réutilisation d'aliments et un coût non calculable
sans choix commercial explicite.

## Livraison et configuration

API `Supabase:Url`/audience, `Cors:AllowedOrigins`, `ConnectionStrings:Postgres`
ou `DATABASE_URL`. Angular `VITE_LIFEOS_API_URL`, `VITE_SUPABASE_URL`,
`VITE_SUPABASE_ANON_KEY` dans `config.js` public, clé anonyme uniquement.
Métadonnées de build compilées séparément ; `/api/version` public et no-store.
Routes Angular hash, base `/LifeOS/` pour Pages et `/` pour nginx.

Migrations incrémentales appliquées au démarrage API sauf `SkipDatabaseMigration=true`.
Sauvegarde PostgreSQL avant livraison avec migration, rollback par restauration.
Docker publie API et Angular ; Pages construit aussi Angular. Un build ou merge
ne prouve pas le déploiement : le refresh appartient à l'utilisateur.
