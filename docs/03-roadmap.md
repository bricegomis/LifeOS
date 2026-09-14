# LifeOS — Roadmap fonctionnelle

Cette roadmap décrit l'ordre de découverte et de livraison envisagé. Elle ne
vaut pas décision d'architecture : le premier jalon est un audit de l'existant.

| Étape | Objectif |
| --- | --- |
| 1. Audit technique | Évaluer l'existant, ses éléments sains, ses limites et les migrations nécessaires avant toute architecture cible définitive. |
| 2. Modèle repas et planning | Reconstruire un modèle cohérent en ne préservant des éléments existants que les bases saines liées aux articles et magasins. |
| 3. Bibliothèque alimentaire | Livrer recettes, composants, portions et macros corrigibles. |
| 4. Planning hebdomadaire | Ajouter contexte foyer, bureau/télétravail, sport et calculs associés. |
| 5. Scénarios et suggestions | Produire des propositions déterministes, explicables et modifiables. |
| 6. Stock et courses | Ajouter stock manuel et liste de courses consolidée. |
| 7. Prix magasins | Introduire comparaison de prix et historique par magasin. |
| 8. Approfondissements | Étudier batch cooking et congélation avancés, micronutriments, tickets et connecteurs sport. |

Les éléments des étapes 7 et 8 ne font pas partie du MVP. Chaque étape doit être
réévaluée à l'aune de l'usage réel et de la réduction effective de la charge
mentale.

## Roadmap technique

L'audit technique (étape 1 ci-dessus) est consigné dans
[`docs/architecture/00-technical-audit.md`](architecture/00-technical-audit.md),
et les décisions d'architecture qui en découlent dans
[`docs/architecture/01-target-architecture.md`](architecture/01-target-architecture.md),
[`docs/architecture/02-data-model.md`](architecture/02-data-model.md) et les
ADR du dossier [`docs/architecture/decisions/`](architecture/decisions/).

| Jalon | Objectif technique |
| --- | --- |
| 1. Fondations | Foyer + PostgreSQL réelle + EF Core, avec un domaine existant porté de bout en bout. |
| 2. Modèle repas/planning | Modèle repas / recettes / planning et moteur de génération backend, testé. |
| 3. Bibliothèque alimentaire | Bibliothèque alimentaire persistée et intégration Open Food Facts. |
| 4. Contexte et nutrition | Contexte foyer, portions par membre, nutrition et sport. |
| 5. Scénarios | Scénarios de suggestion déterministes. |
| 6. Stock et courses | Stock manuel et liste de courses consolidée. |
| 7. Prix magasins V2 | Comparaison de prix et historique par magasin. |
| 8. Approfondissements | Batch cooking avancé, micronutriments, tickets, connecteurs sport. |

Cette roadmap technique correspond aux étapes fonctionnelles ci-dessus, avec
un niveau de détail supplémentaire côté implémentation.
