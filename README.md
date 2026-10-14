# LifeOS
Planifier les repas et le sport, conserver les achats et suivre les prix sans automatisme.

## Documentation produit

- [Vision produit](docs/00-vision.md)
- [Cadrage fonctionnel du MVP](docs/01-scope-and-versions.md)
- [Concepts fonctionnels](docs/02-core-model.md)
- [Roadmap fonctionnelle](docs/03-roadmap.md)
- [UX du semainier manuel](docs/03-week-planner-ux.md)
- [État du projet et mémoire historique](docs/PROJECT.md)

La cible active est `src/frontend-angular` + `src/backend` (PostgreSQL),
avec Supabase pour l'authentification uniquement. Angular est le seul frontend.
La bibliothèque met les **aliments génériques** au premier plan : nutrition
manuelle et filtre « À compléter », recettes sur aliments, produits commerciaux
secondaires pour les prix. Les tickets JSON sont archivés puis validés ligne
par ligne ; aucune nutrition du document n'est importée. Stock, liste de courses
et enrichissement automatique sont retirés du MVP. Le calendrier alimentation/sport,
les portions personnelles, la préparation enfants et les snapshots sont conservés.
L'interface Angular emploie des contrôles natifs et les styles LifeOS, sans
runtime PrimeNG/PrimeUI ; PrimeIcons (MIT) reste utilisé pour la navigation.

Installation et contrôles : [frontend Angular](src/frontend-angular/README.md)
et [API/PostgreSQL](src/backend/README.md). La validation locale ne constitue
pas une vérification du déploiement en production.

La migration `FoodsAndReceipts` sépare le catalogue unifié sans fusion des
homonymes, sans perte des prix/alias/sources et sans réécriture des snapshots.
Les produits sans preuve alimentaire restent non liés, à compléter manuellement.
**Sauvegarder PostgreSQL avant de rafraîchir les containers** : les migrations
s'appliquent au démarrage API (ou séparément avec `SkipDatabaseMigration=true`).
Retour arrière par restauration, pas par migration descendante destructive.
Voir [ADR 0006](docs/architecture/decisions/0006-foods-products-receipts.md).

## Documentation architecture

- [Audit technique](docs/architecture/00-technical-audit.md)
- [Architecture cible](docs/architecture/01-target-architecture.md)
- [Modèle de données](docs/architecture/02-data-model.md)
- [Décisions d'architecture (ADR)](docs/architecture/decisions/)
- [Configuration Google OAuth / Supabase Auth](docs/google-oauth.md)
- [iPhone : installation Xcode gratuite, widgets et renouvellement à 7 jours](docs/ios-v1.md)

Les builds iOS sans signature ne prouvent pas une installation sur iPhone.
La signature Personal Team de l'app et du widget, les permissions et les parcours
réels sont à valider manuellement ; aucune publication App Store/TestFlight requise.
