# LifeOS

Planifier les repas, le sport, les choses à faire et la santé d'un foyer, pour
n'avoir plus qu'à faire. Aujourd'hui, seule la partie alimentation existe, avec les
séances types de sport.

## Documentation

| Document | Contenu |
| --- | --- |
| [Vision](docs/00-vision.md) | Ce que LifeOS doit devenir, et ses principes |
| [Roadmap](docs/01-roadmap.md) | Les six étapes, dans l'ordre |
| [État actuel](docs/PROJECT.md) | Ce qui existe réellement dans le code |
| [Concepts de l'alimentation](docs/02-core-model.md) | Vocabulaire et règles du modèle en place |
| [Architecture](docs/architecture/overview.md) | Schéma d'ensemble et liste des décisions (ADR) |
| [Leçons du premier semainier](docs/semainier-retour-experience.md) | Pourquoi il a été retiré, besoin sport exprimé |
| [Version d'intégration](docs/integration-environment.md) | Essayer le travail en cours sans toucher à la production |
| [Connexion Google](docs/google-oauth.md) | Client Google, clés des jetons et variables de déploiement |
| [iPhone](docs/ios-v1.md) | Installation Xcode gratuite, widgets, renouvellement à 7 jours |

## Code

- [Frontend Angular](src/frontend-angular/README.md) — Angular 22, empaqueté pour
  iPhone avec Capacitor.
- [API](src/backend/README.md) — ASP.NET Core, EF Core, PostgreSQL.
- L'API connecte avec Google et émet ses propres jetons OAuth ([ADR 0011](docs/architecture/decisions/0011-conversational-assistant-mcp.md)).

Une fusion dans `main` publie les images Docker et vaut déploiement. Sauvegarder
PostgreSQL et les volumes de fichiers avant de rafraîchir les containers : les
migrations s'appliquent au démarrage de l'API. Une validation locale ne prouve pas
que la production fonctionne.
