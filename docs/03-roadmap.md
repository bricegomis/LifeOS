# LifeOS — Livraison du MVP semainier manuel

Plan approuvé le 7 octobre 2026 : documentation d'abord, puis tous les lots
séquentiellement sans nouvelle approbation intermédiaire. Les lots ci-dessous
sont implémentés dans l'interface Angular et l'API, sans refonte Vue.

| Lot | Objectif et critère de sortie | État |
| --- | --- | --- |
| 0 | Cadrage, vocabulaire, UX, décisions et distinction cible/existant versionnés. | Documenté |
| 1 | Inspection Angular/API/OpenAPI/calculs/migrations/tests ; ADR des horaires, portions, snapshots, raccord achats et migration non destructive. | Implémenté |
| 2 | Produits et recettes persistés, CRUD/recherche/archivage, ingrédients/unités/portions/nutrition ; achats/prix préservés. | Implémenté |
| 3 | Catalogue sportif par foyer, CRUD/recherche/archivage ; durée positive, calories non négatives, aucun import silencieux. | Implémenté |
| 4 | API événement horaire, CRUD/déplacement, recette ou produits, occurrences sportives, préparation enfants et snapshots ; historique et isolation préservés. | Implémenté |
| 5 | Semainier Angular desktop 6–20 et journée mobile, bibliothèques, formulaires accessibles, Aujourd'hui/résumés, hors plage/chevauchements/à positionner, erreurs explicites. | Implémenté |
| 6 | Accueil/navigation/création manuels ; retirer génération, alternance et règles du parcours sans supprimer les données. | Implémenté |
| 7 | Domaine/API/PostgreSQL/frontend, migration vierge et existante, redémarrage, deux foyers, inspection desktop/mobile bornée, documentation et contrôles de publication. | Validé localement ; fusion soumise à CI/review |

Les critères fonctionnels sont dans [le cadrage](01-scope-and-versions.md).
La cible est Angular/API/PostgreSQL, pas une seconde refonte Vue.
Réutiliser semaines/journées et les domaines existants, migrations incrémentales,
relations fortes et auth/JWT/isolation. Pas de framework spéculatif.

## Autorisations et limites

L'utilisateur a ensuite explicitement autorisé **tous les merges automatiques**
dans l'ordre des dépendances, sans attendre sa recette finale. Chaque merge
reste conditionné aux tests/checks et protections/reviews du dépôt ; aucune
protection contournée ni CI rouge ignorée. Corriger les conflits sans perdre
les changements d'autrui.
Cette autorisation ne couvre ni suppression/reset de données, ni intervention
manuelle en production. Les workflows existants déclenchés par les merges
restent applicables. Distinguer branche, fusion, livraison et déploiement vérifié.

Signaler une décision produit réellement non résolue ou un blocage de validation,
sans inventer une règle métier ou une réussite. Les achats/stock existants sont
préservés, pas étendus. Automatisation, semaines types, nouvelles catégories
d'événement, fatigue et connecteurs sont reportés.

## Références techniques

[Audit historique](architecture/00-technical-audit.md),
[architecture](architecture/01-target-architecture.md),
[modèle de données](architecture/02-data-model.md),
[ADR](architecture/decisions/). Les anciennes roadmaps de génération sont
remplacées par les lots ci-dessus ; leurs implémentations/données ne sont pas
implicitement supprimées.

## Validation et limites

Suite backend Release sur PostgreSQL 16 via Testcontainers (38 tests domaine,
57 intégration), 26 tests frontend, type-check/lint/build Angular et absence de
changement EF non migré. Upgrade réel depuis `SeparateWeekTypes` avec achats,
prix, repas composés et séances historiques préservés, sans horaire inventé.
Parcours navigateur desktop 1440 px et mobile 390 px : rechargement, deux
semaines, riz 100 g/200 g préparés, banane, plusieurs séances, chevauchements,
22 h, nutrition inconnue, panne API/retry et édition/suppression mobile.

Les contrôles emploient une authentification synthétique uniquement en
environnement Testing, pas une session Supabase de production. Un navigateur
local et un merge ne prouvent pas le déploiement VPS. Aucune base utilisateur
n'a été effacée et aucune opération manuelle de production n'est incluse.
