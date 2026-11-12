# Version d'intégration

La version d'intégration permet d'essayer le travail en cours, y compris sur
téléphone, sans toucher à la production. Elle suit la branche `integration`
(voir [roadmap](01-roadmap.md), « Branches et livraison »).

## Fonctionnement

| | Production | Intégration |
| --- | --- | --- |
| Branche | `main` | `integration` |
| Images | `lifeos-api:latest`, `lifeos-web:latest` | `lifeos-api:integration`, `lifeos-web:integration` |
| Fichier compose | `docker-compose.yaml` | `docker-compose.integration.yaml` |
| Containers | `lifeos-api`, `lifeos-web` | `lifeos-integration-api`, `lifeos-integration-web` |
| Volumes | `lifeos_receipts`, `lifeos_food_images` | `lifeos_integration_receipts`, `lifeos_integration_food_images` |
| Base | base de production | **base séparée** |

Chaque fusion sur `integration` publie les images `:integration`. Watchtower les
récupère comme pour la production. Les migrations s'appliquent au démarrage de
l'API d'intégration.

## Mise en place sur le VPS (une seule fois, par l'utilisateur)

1. **Créer une base PostgreSQL séparée**, avec son propre utilisateur, par exemple
   `lifeos_integration`. Ne jamais réutiliser la base de production : les
   migrations d'intégration peuvent être destructives.
2. **Choisir deux adresses**, par exemple `lifeos-int.example.com` pour l'interface
   et `api-lifeos-int.example.com` pour l'API, et les router vers les containers
   `lifeos-integration-web` (port 80) et `lifeos-integration-api` (port 8080),
   comme pour la production.
3. **Créer la pile** à partir de `docker-compose.integration.yaml`, avec son
   propre fichier d'environnement. Mêmes variables que la production
   (`.env.example`), avec ces différences :
   - `POSTGRES_CONNECTION_STRING` : la base d'intégration ;
   - `FRONTEND_URL` : l'adresse de l'interface d'intégration ;
   - `LIFEOS_API_URL` : l'adresse de l'API d'intégration ;
   - les variables Supabase restent identiques : même projet, mêmes comptes.
4. **Supabase → Authentication → URL Configuration** : ajouter l'adresse de
   l'interface d'intégration aux « Redirect URLs » (par exemple
   `https://lifeos-int.example.com/**`).
5. Démarrer la pile après la première publication des images `:integration`.

## À savoir

- Le compte de connexion est le même qu'en production, mais les données sont
  distinctes : un foyer vide est créé à la première connexion.
- La base d'intégration peut être vidée ou recréée à tout moment. Rien de ce qui
  s'y trouve n'est à conserver.
- L'application iPhone installée reste branchée sur la production. La version
  d'intégration s'essaie dans le navigateur du téléphone.
- Le bas du menu affiche le numéro de build de l'interface et de l'API : il permet
  de vérifier que la version d'intégration est bien à jour.
