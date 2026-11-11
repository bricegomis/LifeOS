# Semainier et sport — leçons de la première version

Rédigé le 2026-11-08, resserré le 2026-11-11. Le premier semainier, saisi à la
main, n'a pas été utilisé et a été supprimé
([ADR 0008](architecture/decisions/0008-remove-week-planner.md)). Ce document
garde ce qu'il faut en retenir pour les étapes 2 et 4 de la
[roadmap](01-roadmap.md). La cible est décrite par
l'[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md).

## Pourquoi il n'a pas été utilisé

Retour d'usage :

- **Rien n'était préparé d'avance.** Aucune semaine n'existait tant qu'on ne
  cliquait pas pour la créer, et elle arrivait vide : tout était à saisir.
- **La visualisation ne donnait pas envie** de s'en servir.
- **Les catalogues étaient confus.** Aliments, produits, articles, recettes et
  préparations ponctuelles se recoupaient.

Constats tirés du code :

- Une seule boîte de dialogue mélangeait jour, horaires, recette, préparation
  ponctuelle, rendement, portions, répartition par personne, coefficients et avis.
- Les heures de début et de fin étaient à saisir pour chaque repas, sans valeur
  par défaut.
- Modifier un repas imposait « Remplacer le contenu » : la rigidité des snapshots
  se voyait.
- Choix par listes déroulantes natives, filtre caché sous « Plus d'options ».
- Trois vues des mêmes données ; les quantités à préparer étaient enfouies.
- Les avertissements « nutrition incomplète » étaient partout, puisque beaucoup
  d'aliments sont « À compléter ».
- On pouvait dupliquer une semaine entière, mais pas reprendre un repas récent.
- Une séance de sport n'existait que dans une journée du semainier.
- La lecture d'une semaine coûtait environ 45 requêtes, et la nutrition était
  calculée dans les DTO de l'API au lieu du domaine.

## Ce qui reste valable

- Isolation par foyer sur chaque ligne, références croisées rejetées.
- Snapshot : modifier une recette ne réécrit jamais ce qui est déjà planifié.
- Quantité à préparer = `quantité de l'ingrédient × portions prévues / rendement`.
- Une valeur inconnue reste inconnue : jamais remplacée par zéro, total marqué incomplet.
- Les repas choisissent des aliments et des recettes, jamais un produit ou une marque.

## Sport : besoin exprimé (2026-11-10)

À reprendre au cadrage de l'étape 2.

- Définir des séances types (existe déjà : `SportTemplate`).
- Enregistrer les séances réalisées à une date, sans semaine ni journée à créer.
- Gérer la récurrence (par exemple course le mardi et le jeudi).
- Suivre semaine par semaine les calories dépensées.
- **Confirmation explicite.** La récurrence prévoit des séances attendues ; seules
  les séances confirmées comptent dans le bilan.
- **Calories saisies par séance.** La valeur de la séance type est proposée ; la
  séance confirmée porte sa propre valeur. Pas de formule durée × intensité × poids.
- **Suivi par personne.** Une séance appartient à un profil. Seul l'utilisateur
  principal est suivi au début.
- Plus tard : import depuis Garmin, Strava ou Apple Santé, avec sa propre ADR.
  Le modèle doit seulement permettre d'indiquer la source d'une séance.
