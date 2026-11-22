# ADR 0017 — Tâches : todos, routines du foyer et projets

Statut : accepté (cadrage), 2026-11-22. Étape 3 de la [roadmap](../../01-roadmap.md).
Maquette à valider avant tout code. Applique l'[ADR 0009](0009-modular-monolith-and-planning-core.md)
(module Tâches, schéma `tasks`) et l'[ADR 0016](0016-angular-material-ui-kit.md) (UI kit).

## Contexte

Besoin : sortir de la tête ce qu'il y a à faire, ponctuel ou récurrent, et le
suivre au quotidien sans attendre le semainier. Le semainier (étape 4) placera
ensuite ces tâches dans la semaine ; le module doit donc déjà porter ce dont il
aura besoin (échéance, durée, priorité).

## Décision

Le module Tâches porte trois notions : les **todos**, les **routines** et les
**projets**, plus deux aides de saisie : la **boîte de réception** et les
**étiquettes**. Toutes les données sont dans le schéma `tasks`, filtrées par foyer.

### Todos

Un todo est une chose à faire une fois. Il porte :

- un titre ;
- une image facultative, aux mêmes règles que les images d'exercices ; sans image,
  la carte affiche la première lettre du titre en majuscule ;
- des notes facultatives en texte libre ;
- une échéance facultative (date, sans heure) ;
- une durée estimée facultative, en minutes ;
- une priorité : haute, normale (par défaut) ou basse ;
- un projet facultatif ;
- des étiquettes facultatives ;
- une checklist facultative : une liste ordonnée de sous-tâches (titre, cochée ou
  non). Cocher toutes les sous-tâches ne termine pas le todo ;
- un profil responsable ;
- un état : ouvert, fait (avec sa date) ou abandonné.

Un todo ouvert dont l'échéance est passée est **en retard**. Il reste visible
jusqu'à ce qu'on le fasse, le reporte (nouvelle échéance) ou l'abandonne. Rien ne
glisse tout seul. Un todo fait peut être rouvert.

### Boîte de réception

La saisie rapide ne demande qu'un titre. Le todo créé est marqué **à trier**. Il
apparaît dans la boîte de réception, pas dans « Aujourd'hui ». Le trier, c'est
l'ouvrir et l'enregistrer depuis sa fiche (échéance, projet, etc., même vides) :
la marque disparaît. Un todo à trier peut aussi être fait ou abandonné directement.

### Routines

Une routine est une chose à refaire, comme sortir les poubelles, changer les
draps ou payer une facture. Elle porte un titre, une image facultative (même
règle que les todos), des notes, une durée estimée, une priorité, des étiquettes
et un profil responsable, et sa **récurrence**, au choix :

- **date fixe** : tous les N jours, certains jours de la semaine toutes les N
  semaines, ou un jour du mois tous les N mois (le 31 tombe le dernier jour d'un
  mois plus court), à partir d'une date de départ ;
- **délai** : N jours après la dernière fois qu'elle a été faite (ou après la date
  de départ si elle n'a jamais été faite).

La prochaine échéance est calculée à la lecture, jamais stockée. Une routine n'a
qu'une échéance ouverte à la fois : tant qu'elle n'est pas faite ou sautée, elle
est en retard et les échéances suivantes ne s'empilent pas.

Chaque passage est gardé dans l'historique : **fait** (date, note facultative) ou
**sauté** (date). Faire ou sauter clôt l'échéance ouverte ; la suivante est
calculée depuis la règle (date fixe) ou depuis ce passage (délai). Un passage
peut être supprimé, l'échéance est alors recalculée.

**Série** : nombre d'échéances consécutives faites au plus tard le jour de
l'échéance. Une échéance faite en retard ou sautée remet la série à zéro. Pas de
jokers. La fiche affiche aussi la dernière fois et la meilleure série.

Modifier la récurrence vaut pour les échéances suivantes ; l'historique reste.
Une routine peut être mise en pause (aucune échéance, série conservée) ou arrêtée
(historique conservé).

La checklist reste propre aux todos.

### Projets

Un projet regroupe des todos. Il porte un nom, une image facultative (même règle),
des notes, une échéance facultative et un état (en cours, terminé, abandonné). Son
avancement est calculé : todos faits sur todos non abandonnés. Ni étapes, ni
dépendances, ni jalons. Terminer un projet ne touche pas ses todos ouverts : l'écran
les signale avant de confirmer.

### Étiquettes

Étiquettes libres du foyer, comme celles des aliments, posées sur les todos et les
routines. Elles servent de filtre (maison, administratif, voiture…).

### Personnes

Chaque todo et chaque routine porte un `profile_id` dès sa création. Seul
l'utilisateur principal est proposé pour l'instant ; les tâches des enfants
viendront plus tard sans changement de modèle.

### Écrans

Menu « Tâches » dans la navigation, avec :

- **Aujourd'hui** (page d'accueil du module) : en retard, aujourd'hui, puis les 7
  prochains jours, todos et routines mêlés sous forme de cartes (image ou
  initiale, titre, échéance, durée, priorité, projet, étiquettes, série pour une
  routine). Une coche marque fait ; la routine propose aussi « sauter ». Filtre
  par étiquette. Les todos ouverts sans échéance sont dans une section repliée
  « Sans échéance » ;
- **Boîte de réception** : champ de saisie rapide et liste à trier ;
- **Toutes les tâches** : todos et routines dans une seule liste, avec un champ de
  recherche (titre, notes, étiquettes, projet, sous-tâches, sans tenir compte des
  accents ni des majuscules) et des filtres : type (todos, routines), état (ouverts,
  faits, abandonnés ou arrêtés, tous) et étiquette. Les tâches à trier y apparaissent
  aussi. Tri par échéance, sans échéance ensuite, faits à la fin ;
- **Routines** : liste, puis une page par routine (récurrence, prochaine
  échéance, série, historique) ;
- **Projets** : liste avec avancement, puis une page par projet avec ses todos ;
- une page par todo, modifiée par des popups, selon l'ADR 0015 et l'ADR 0016.

Les écrans sont pensés pour l'ordinateur et le smartphone.

### Assistant

Le module livre ses outils MCP (ADR 0011), en lecture et en écriture : lister,
créer, modifier, faire, rouvrir, abandonner un todo et sa checklist ; saisie
rapide ; créer, modifier, faire, sauter, mettre en pause et arrêter une routine ;
créer, modifier et clore un projet ; image par URL.

### Contrat de lecture

Le module publie pour le futur semainier : les todos ouverts avec échéance, durée,
priorité et profil ; les échéances ouvertes des routines sur une période ; les
passages faits par jour et par profil.

## Conséquences

- Nouveau schéma `tasks` et nouveau volume d'images `lifeos_task_images`, à
  sauvegarder comme les autres volumes avant une mise à jour.
- Aucune dépendance vers les autres modules ; le test d'architecture le vérifie.
- Durée et priorité ne servent qu'à l'affichage et au tri jusqu'au semainier.
- Hors périmètre : rappels et notifications (étape 6), tâches des enfants,
  dépendances entre tâches, checklist de routine, partage avec d'autres adultes.
