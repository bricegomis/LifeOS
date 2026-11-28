# ADR 0022 — Lecture : une bibliothèque de livres lus, à lire ou écartés

Statut : accepté, 2026-11-28. Étape 8 de la [roadmap](../../01-roadmap.md). Maquette validée le 2026-11-28 (canevas des
maquettes, « Lecture — ordinateur » et « Lecture — smartphone ») ; livré en code le même jour.
Applique l'[ADR 0009](0009-modular-monolith-and-planning-core.md) (module à part, contrat publié),
l'[ADR 0011](0011-conversational-assistant-mcp.md) (outils de l'assistant, l'API n'appelle aucun
service externe) et l'[ADR 0016](0016-angular-material-ui-kit.md) (UI kit).

## Contexte

L'utilisateur veut savoir ce qu'il a lu, ce qu'il veut lire et ce qu'il a écarté, et que Claude
puisse lui conseiller la prochaine lecture. Pour une première version, il ne veut ni minutes, ni
pages, ni séances de lecture : seulement une liste de livres bien tenue.

## Décision

### Périmètre de la V1

- Une **bibliothèque de livres**, avec un état par livre.
- Pas de séances de lecture, de minutes, de pages lues ni d'avancement en pourcentage.
- Pas d'objectif en V1. L'objectif « temps de lecture par jour » (série et jokers, comme les
  défis du Sport) est prévu pour une V2, avec les séances ; il demandera son propre cadrage.
- Un profil sur chaque donnée ; seul l'utilisateur principal est affiché (comme le Sport). Les
  données sont filtrées par foyer.

### Livre

- **Titre** (obligatoire), **auteurs** (texte libre, un ou plusieurs), **couverture** facultative
  (image stockée comme celles des aliments, trouvée par Claude ou envoyée à la main).
- **Format** : papier, liseuse, audio ou BD / manga.
- **Genres** : étiquettes libres du foyer, plusieurs par livre.
- **Série** facultative et **numéro de tome** (décimal permis : 1, 2, 2.5).
- **Notes** libres et **citations** (texte, emplacement facultatif en texte libre : « p. 42 »,
  « chap. 3 »).

### État et lectures

Un livre a un **état** affiché, calculé à partir de ses lectures :

| État | Quand |
| --- | --- |
| À lire | aucune lecture, et le livre n'est pas écarté |
| Pas envie | aucune lecture, écarté exprès (raison facultative) |
| En cours | une lecture est commencée et pas terminée |
| Lu | la dernière lecture est terminée |
| Arrêté | la dernière lecture est arrêtée (raison facultative) |

Une **lecture** a une date de début et une date de fin facultatives pour une lecture passée (on peut ajouter un livre
lu il y a longtemps sans dates, ou avec l'année seule saisie comme 1er janvier ; une lecture en cours a toujours son
jour de début), une issue (en cours, lu, arrêté), une **note** facultative de 0 à 10 et un **avis** facultatif. 0 est une note, pas
« non noté ». Relire un livre lu ajoute une nouvelle lecture ; la fiche montre toutes les
lectures datées et la dernière note. Une seule lecture en cours à la fois par livre.

« Pas envie » ne vaut que pour un livre jamais commencé ; il se retire (le livre revient à « À lire ») et commencer une
lecture d'un livre écarté le retire aussi. Une lecture se corrige (dates, note, avis, raison) ou se retire ; un livre et
une série se suppriment (les livres d'une série supprimée restent, sans série).

### Série

Une série a un nom, un nombre de tomes facultatif et ses livres. Sa page montre les tomes dans
l'ordre avec leur état ; quand le nombre de tomes est connu, les numéros entiers sans livre
apparaissent comme **manquants**.

### Écrans (menu : domaine « Loisirs », entrée « Lecture »)

- **Bibliothèque** : onglets par état (Tous, En cours, À lire, Lus, Arrêtés, Pas envie),
  recherche texte sans accents ni majuscules (titre, auteur, série), filtres par format, genre
  et série ; grille de couvertures ou liste ; bouton d'ajout.
- **Fiche livre** : couverture, état et actions (Commencer, Terminé, Arrêter, Relire, Pas envie),
  lectures datées avec note et avis, citations, notes ; modifications en popups.
- **Page par série** : tomes dans l'ordre, lus, à lire et manquants.

Pas de bilan en V1. L'accueil « Aujourd'hui » ne change pas : il n'y a rien à cocher chaque jour.

### Assistant (outils MCP)

- Rechercher, créer et modifier livres, séries, lectures et citations ; changer l'état ;
  couverture par URL. Les suppressions de livres et de séries restent à l'interface.
- **Conseil** : Claude lit la bibliothèque, les notes, les avis et les « pas envie », puis
  propose ; il peut ajouter les livres proposés à « À lire ».
- **Import d'une liste** : l'utilisateur colle une liste ou une capture (Goodreads, Babelio…),
  Claude crée les livres par les outils. L'API n'importe aucun fichier et n'appelle aucune base
  de livres.
- Claude ne relie jamais un livre à un autre par le nom tout seul : avant de créer, il cherche
  et demande en cas de doute (doublon possible).

## Conséquences

- Nouveau module Lecture (tables et schéma propres, contrat publié, isolation par foyer testée,
  test d'architecture). Nouveau volume d'images `lifeos_book_images`, à ajouter aux sauvegardes.
- Contrat de lecture publié pour le futur semainier : livres en cours et lectures terminées par
  jour et par profil.
- Films et séries (étape 9) rejoindra le domaine « Loisirs » du menu.
- La V2 (séances, minutes, objectif de temps par jour, bilan) demandera son propre cadrage.
