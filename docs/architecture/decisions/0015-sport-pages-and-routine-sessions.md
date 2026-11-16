# ADR 0015 — Sport : une page par élément, passages de routine et ressenti

Statut : accepté et livré en code, 2026-11-16. Cadrage d'un lot ajouté à l'étape 2 de la
[roadmap](../../01-roadmap.md). Maquette validée le 2026-11-16 (canevas des maquettes,
rangée « Sport — pages par élément, passages et ressenti »). Étend
l'[ADR 0013](0013-sport-module.md) et l'[ADR 0014](0014-sport-daily-routines.md).
Les écrans suivent le kit Angular Material et ses conventions
([ADR 0016](0016-angular-material-ui-kit.md)) : la « fenêtre de modification » est
une fenêtre de dialogue sur ordinateur et un écran plein sur téléphone.

## Contexte

Retours après les premiers jours d'usage (2026-11-16) :

- la page « Réglages du sport » empile objectifs, défis, séances types, routines et
  exercices ; on n'y trouve pas où modifier un exercice, et chaque formulaire
  s'ouvre sous sa liste ;
- le menu liste toutes les pages à plat, et le téléphone navigue par des onglets en
  bas, qui ne peuvent pas accueillir les nouvelles pages ;
- la photo d'un exercice ne s'ajoute qu'après un premier enregistrement ;
- une routine courte (« Boulot », 5 minutes, sans matériel) se fait plusieurs fois
  par jour, mais un exercice ne se coche qu'une fois par jour : seul le premier
  passage est enregistré ;
- rien ne permet de noter comment un exercice s'est passé (effort, gêne), ce qui
  sert à décider d'augmenter ou non une charge.

## Décision

### Une page par élément

La page « Réglages du sport » disparaît. Chaque élément a sa page : **Exercices**,
**Routines**, **Défis**, **Objectifs**, **Séances types**. Les pages Aujourd'hui,
Historique et Progression restent.

Chaque page suit le même gabarit :

- la liste des éléments, avec un bouton « Ajouter » ;
- un appui sur un élément ouvre sa **fenêtre de modification** (popup) ; la création
  utilise la même fenêtre ;
- arrêter ou archiver se fait depuis cette fenêtre, avec confirmation ;
- les éléments arrêtés ou archivés sont masqués par défaut, affichables par un filtre.

Tout se crée et se modifie sur ordinateur comme sur téléphone, séances types
comprises.

**Faire une routine** part de deux endroits : la carte de la routine sur la page
Aujourd'hui (« Commencer », « Continuer le passage », « Nouveau passage ») et, sur
la page Routines, un bouton « Faire » ou « Continuer » sur chaque routine en cours.
Les deux mènent à la page « Faire la routine », sur ordinateur comme sur téléphone.

La page « Faire la routine » porte un bouton « Modifier la routine », qui ouvre la
même fenêtre que la page Routines. L'accueil Sport renvoie vers la page Routines.

Les **objectifs deviennent modifiables** (nombre visé), avec la règle de l'ADR 0013 :
le changement vaut à partir de la semaine en cours. La forme d'un objectif (par
séance type, par sport, global) ne change pas ; on l'arrête et on en crée un autre.

### Menu rangé par domaine, aussi sur téléphone

Cette partie touche toute l'application, pas seulement le Sport.

Le menu de gauche a un item par domaine, **Alimentation** et **Sport**, qui s'ouvre
pour montrer ses pages :

- Alimentation : Recettes, Aliments, Produits et prix, Magasins, Tickets ;
- Sport : Aujourd'hui, Historique, Progression, Routines, Exercices, Défis,
  Objectifs, Séances types.

Le domaine de la page affichée est ouvert à l'arrivée ; chaque item s'ouvre et se
referme d'un appui. Paramètres et le compte restent en bas du menu.

**Sur téléphone, ce même menu remplace les onglets du bas** et les sous-onglets du
Sport : un bouton en haut de l'écran l'ouvre en panneau à gauche, et choisir une
page le referme. La barre du haut rappelle la page affichée (« Sport · Routines »).

### Photo à la création d'un exercice

**La photo d'un exercice l'accompagne partout où il est affiché** : liste des
exercices, lignes de la fenêtre d'une routine, carte de l'exercice pendant un
passage, écran de ressenti. Elle occupe la gauche de la carte sur toute sa hauteur ;
sans photo, un pictogramme d'image en tient lieu.

La fenêtre d'un exercice contient la photo dès la création : elle est choisie dans
le formulaire et enregistrée avec l'exercice. Par l'assistant, `create_exercise`
accepte l'URL d'une image dans le même appel. L'API ne fabrique et ne cherche
aucune image.

### Charge facultative

Un exercice n'est plus déclaré « avec charge » ou non. La charge est une valeur
facultative, en kg par pas de 0,5 : vide ou 0 veut dire « au poids du corps ».

- La fiche d'un exercice porte une charge facultative, proposée comme cible quand
  l'exercice est ajouté à une routine.
- Chaque ligne de routine a sa propre charge, facultative elle aussi : le même
  exercice peut se faire au poids du corps dans une routine et lesté dans une autre.

### Passages de routine

Une routine peut être faite plusieurs fois le même jour. Chaque fois est un
**passage** : routine, jour, heure de début, heure de fin, commentaire facultatif.
Les coches d'exercices appartiennent à un passage, et non plus à un jour : un
exercice se coche une fois par passage.

- **Une coche par série, par un compteur.** Chaque exercice a un seul bouton
  « Série faite » avec son compteur : pour « Chaise, 4 × 45 s », chaque appui
  valide une tenue de 45 s et fait passer de 0 / 4 à 1 / 4, puis 2 / 4… Le fond du
  bouton se remplit de gauche à droite en proportion (un quart par série ici). À
  4 / 4, le bouton devient plein et s'appelle « Exercice fait ». Un petit bouton à côté retire la dernière
  série en cas d'appui de trop. L'exercice est **fait** quand toutes ses
  séries sont cochées. On avance ainsi dans la routine série après série, dans
  l'ordre que l'on veut.
- **Pas de correction pendant le passage** : une coche enregistre la valeur et la
  charge de la cible. L'écran ne propose plus de saisir une autre valeur ou une
  autre charge ; l'API et l'assistant le peuvent encore. Pour faire évoluer les
  chiffres, on modifie la cible de la routine.
- L'effort et la gêne se notent **une fois par exercice** et valent pour toutes ses
  séries ; seul le nombre de séries faites est compté.
- Un passage est **terminé** quand tous les exercices de la routine ce jour-là sont
  faits ; l'heure de fin est celle de la dernière coche. Cet état se déduit des
  coches, il n'est pas saisi. Tant que le passage est en cours, une série se
  décoche ; **un passage terminé ne se rouvre pas**, on le supprime et on le refait.
- **Un seul passage en cours par routine.** « Nouveau passage » n'est proposé que
  si le précédent est terminé. Un passage se supprime, avec ses coches.
- Pas de bouton « Tout cocher » : un passage se remplit série après série.
- Un passage resté inachevé à minuit est gardé tel quel ; il ne se reprend pas le
  lendemain.
- **Pas d'objectif de passages par jour.** La routine est faite pour le jour dès
  qu'un passage est terminé, comme aujourd'hui ; les passages suivants sont comptés,
  sans rien changer à la série ni aux jokers.
- Le calendrier colore un jour selon son meilleur passage (part d'exercices faits)
  et affiche le nombre de passages terminés quand il dépasse 1.
- L'accueil affiche, par routine prévue, les passages du jour avec leur heure
  (« 2 passages : 9 h 12, 14 h 40 »).
- La progression d'une routine ajoute le nombre de passages par jour sur les
  dernières semaines. La progression d'un exercice (courbe, records) prend tous
  les passages où au moins une de ses séries est cochée.

### Ressenti par exercice

Chaque exercice d'un passage peut porter, en plus de sa valeur et de sa charge :

- un **effort** perçu, de 1 à 10 ;
- une **gêne**, de 0 à 10 ; 0 veut dire « aucune gêne », différent de « non noté ».

Quand un passage se termine, l'écran propose de noter les exercices. Chaque note
se règle par un **compteur** : « − » à gauche pour diminuer, « + » à droite pour
augmenter. Il part d'une **valeur proposée**, 7 pour l'effort et 2 pour la gêne.
« Enregistrer » garde les valeurs affichées pour tous les exercices ; « Passer »
n'enregistre aucun ressenti, et on peut y revenir le jour même. Le commentaire du
passage se saisit au même endroit.

Les valeurs proposées sont un **réglage du profil**, modifiable sur la page
Routines avec les mêmes compteurs.

La gêne est une **donnée d'entraînement** du module Sport : visible du foyer et
lisible par l'assistant, comme le ressenti d'une séance réalisée. Le diagnostic, les
soins et le suivi médical restent au module Santé, propre au profil et jamais
transmis à l'assistant.

L'effort et la gêne s'affichent dans la progression de l'exercice, à côté de la
valeur et de la charge. **La cible reste réglée à la main** : l'application ne
propose pas de l'augmenter.

### Assistant

- `create_exercise` accepte une URL d'image.
- `complete_exercise` coche dans le passage en cours du jour, ou en ouvre un ; il
  accepte le nombre de séries faites (toutes par défaut), l'effort et la gêne.
  `uncomplete_exercise` vise un passage.
- Nouvel outil pour enregistrer un passage entier en un appel : routine, date, heure
  facultative, exercices avec valeur, charge, effort et gêne, commentaire.
- Nouvel outil pour supprimer un passage.
- `list_routines` renvoie les passages du jour ; `get_exercise_progress` renvoie
  l'effort et la gêne des dernières coches.
- Un objectif de la semaine se modifie par `change_weekly_goal`, déjà présent.

### Contrat publié

L'état des routines du jour gagne le nombre de passages terminés.

## Hors périmètre

- Objectif de passages par jour, jour « partiel », seuil de jour réussi.
- Rappels au fil de la journée (étape 6, notifications).
- Progression automatique ou suggérée de la charge : version ultérieure.
- Valeur, charge ou ressenti différents d'une série à l'autre ; temps de repos.
- **Mode « démarrer une routine »**, prévu pour l'itération suivante, une fois cette
  base en place : une page dédiée sur téléphone qui déroule la routine, avec un
  chronomètre pour un exercice tenu et un bouton « Fait » pour un exercice en
  répétitions. Il s'appuiera sur les passages et la coche par série décrits ici.

## Conséquences

- Migration additive `SportRoutineSessions` : nouvelle table des passages ; les
  coches gagnent leur passage, le nombre de séries faites, l'effort et la gêne.
  L'unicité passe de (routine, exercice, jour) à (passage, exercice).
- **Reprise de l'existant** : pour chaque routine et chaque jour ayant des coches,
  un passage est créé et les reçoit ; son début est la première coche, sa fin la
  dernière s'il est complet. Chaque coche existante compte toutes les séries de sa
  cible. Séries de jours, records et calendriers restent identiques.
- L'indicateur « avec charge » d'un exercice est remplacé par une charge
  facultative ; les charges déjà saisies sur les routines sont gardées, une charge
  absente se lit « au poids du corps ». `create_exercise` et `update_exercise`
  prennent la charge à la place de l'indicateur.
- L'ADR 0014 excluait la saisie série par série : elle est levée pour la coche
  seulement.
- L'ADR 0014 est mis à jour à la livraison : « une ligne par exercice et par jour »
  devient « par passage », et la gêne sort de son hors-périmètre.
- Les écrans de réglages du sport (`sport-settings-page`, `routine-settings`) sont
  remplacés par cinq pages et leurs fenêtres de modification.
- La barre d'onglets du téléphone et les sous-onglets du Sport (`sport-nav`) sont
  retirés ; le menu de gauche devient le seul moyen de naviguer, sur les deux
  formats. `docs/PROJECT.md` (paragraphe Navigation) est mis à jour à la livraison.
