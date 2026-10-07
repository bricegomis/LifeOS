# LifeOS
Ne plus avoir à réfléchir tous les jours à quoi manger / quoi faire / quoi acheter

## Documentation produit

- [Vision produit](docs/00-vision.md)
- [Cadrage fonctionnel du MVP](docs/01-scope-and-versions.md)
- [Concepts fonctionnels](docs/02-core-model.md)
- [Roadmap fonctionnelle](docs/03-roadmap.md)
- [UX du semainier manuel](docs/03-week-planner-ux.md)
- [État du projet et mémoire historique](docs/PROJECT.md)

La cible active est `src/frontend-angular` + `src/backend` (PostgreSQL),
avec Supabase pour l'authentification. `src/frontend` est l'interface Vue
historique et n'a pas été refondu. Le MVP manuel est implémenté : calendrier
alimentation/sport, trois bibliothèques, portions personnelles et préparation
enfants séparées, snapshots et migration des anciens repas sans heure inventée.
L'interface Angular emploie des contrôles natifs et les styles LifeOS, sans
runtime PrimeNG/PrimeUI ; PrimeIcons (MIT) reste utilisé pour la navigation.

Installation et contrôles : [frontend Angular](src/frontend-angular/README.md)
et [API/PostgreSQL](src/backend/README.md). La validation locale ne constitue
pas une vérification du déploiement en production.

## Documentation architecture

- [Audit technique](docs/architecture/00-technical-audit.md)
- [Architecture cible](docs/architecture/01-target-architecture.md)
- [Modèle de données](docs/architecture/02-data-model.md)
- [Décisions d'architecture (ADR)](docs/architecture/decisions/)
- [Configuration Google OAuth / Supabase Auth](docs/google-oauth.md)
