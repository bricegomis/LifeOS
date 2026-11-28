# ADR 0023 — Navigation et journée

Statut : proposé, 2026-11-28. Plan 2 de la revue UX du 2026-11-28 ; cadrage fait le même jour,
maquette cliquable « Maquette navigation et journée » en attente de validation. Modifie
l'[ADR 0015](0015-sport-pages-and-routine-sessions.md) (plus de barre d'onglets sur téléphone),
l'[ADR 0016](0016-angular-material-ui-kit.md) (conventions d'écran), l'[ADR 0017](0017-tasks-module.md)
(saisie rapide) et l'[ADR 0021](0021-today-home.md) (accueil rangé par moment).

## Contexte

La revue UX du 2026-11-28 a relevé que, sur téléphone, changer de module coûte trois appuis
(menu ☰, domaine, page) parmi 7 domaines et 36 entrées, que les pages « Aujourd'hui » de
l'accueil, de Sport, de Tâches et de Santé montrent un même todo, défi ou complément sous trois
formes différentes, qu'il faut une fenêtre de 7 à 22 champs pour noter quoi que ce soit, et que
sur ordinateur chaque détail remplace la liste. Le plan 1 (menu stable, barre du haut compacte,
coche immédiate, chargement, tutoiement) est livré ; ce plan traite la navigation et la journée.

## Décision

### Téléphone : barre d'onglets

- Une barre d'onglets en bas remplace le menu ☰ : **Aujourd'hui, Tâches, Sport, Alimentation,
  Plus** (choix de l'utilisateur, 2026-11-28).
- **Plus** ouvre une feuille avec Santé, Budget, Lecture et Paramètres, chacun avec ce qui
  l'attend (prises du jour, opérations à ranger, livres en cours).
- Dans un module, les pages sont des **puces** en haut de page (Sport : Aujourd'hui, Séances,
  Routines, Défis, Progrès, puis « Plus… » pour le reste). Chaque onglet garde sa position de
  défilement.
- Sur ordinateur, le menu latéral reste (un seul domaine ouvert, ADR 0016).

### Une seule ligne à cocher

- Un composant partagé (`app-check-row`) : case à gauche, point de couleur du module, titre,
  détail, action à droite (« Démarrer », « +20 »). Il sert à l'accueil et aux pages du jour de
  Tâches, Sport et Santé.
- Sans photo, plus de pastille d'initiale : le point de couleur suffit.
- **Défi compact** : « +n » refait la dernière série ; « ⋯ » ouvre les autres valeurs et le retrait.
  La carte complète reste sur la page du défi.

### Accueil rangé par moment

- L'accueil range ce qui reste à faire par moment : **En retard, Matin, Dans la journée, Soir**,
  puis la semaine (objectifs, pas). La nuit et l'énergie tiennent en une ligne sous la date.
- Les compléments gardent leur moment. Les todos vont dans « Dans la journée ». Le moment des
  routines du foyer et des routines sportives est à trancher (point ouvert ci-dessous).
- Les pages du jour de Tâches, Sport et Santé **restent** (choix de l'utilisateur), avec la même
  ligne et les mêmes gestes que l'accueil.

### Saisie rapide

- Une barre « Noter… » en bas de l'accueil ouvre une feuille courte :
  - **Todo** : titre et quand (Aujourd'hui, Demain, Ce week-end, Sans date) ;
  - **Symptôme** : type et intensité 1 à 10, maintenant.
- « Détails… » ouvre la fenêtre complète actuelle. Pas de boîte de réception, pas d'analyse du
  texte : la feuille appelle les endpoints de création existants.

### Ordinateur

- **Palette Ctrl K** : aller à une page, chercher dans les modules (par leurs recherches
  existantes) et créer un todo.
- **Panneau de détail** à droite pour les todos, routines du foyer, compléments et opérations du
  budget ; la liste reste en place. Les recettes, aliments et livres gardent leur page.
- Raccourcis : ↑ ↓ change de ligne, Espace coche, Entrée ouvre la page complète, Échap ferme.

## Points ouverts (à valider avec la maquette)

1. Moment facultatif (Matin, Midi, Soir) sur les routines du foyer et les routines sportives, avec
   deux petites migrations ; sans moment, « Dans la journée ».
2. Nombre de puces visibles avant « Plus… » (proposé : 5).
3. Liste des éléments ouverts en panneau sur ordinateur.

## Livraison

Une PR par étape, chacune utilisable seule : barre d'onglets et feuille « Plus » ; puces ; ligne
unique et défi compact ; accueil par moments (et migrations si retenues) ; saisie rapide ;
palette Ctrl K ; panneau de détail. Les docs (ADR 0015, 0016, 0017, 0021, `docs/PROJECT.md`,
`src/frontend-angular/CLAUDE.md`) suivent chaque étape.

## Conséquences

- Le futur widget « à faire aujourd'hui » (étape 6) et la vue « jour » du semainier (étape 4)
  reprendront l'accueil par moments.
- Le menu ☰ ne sert plus que sur ordinateur.
- Aucun module ne lit un autre : l'accueil, la feuille « Plus » et la palette passent par les
  endpoints publiés de chaque module (ADR 0009).
