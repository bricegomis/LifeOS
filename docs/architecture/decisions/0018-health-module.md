# ADR 0018 — Santé : compléments, symptômes, analyses, documents

Statut : accepté (cadrage), 2026-11-22. Étape 5 de la [roadmap](../../01-roadmap.md),
avancée avant le semainier (étape 4) à la demande de l'utilisateur. Maquette à valider
avant tout code. Applique l'[ADR 0009](0009-modular-monolith-and-planning-core.md)
(module Santé, schéma `health`, données privées par profil), l'[ADR 0011](0011-conversational-assistant-mcp.md)
(assistant) et l'[ADR 0016](0016-angular-material-ui-kit.md) (UI kit).

## Contexte

But exprimé le 2026-11-22 : suivre tout ce qui touche à la santé de l'utilisateur. Plus
tard, la santé deviendra centrale : des objectifs de santé pourront proposer des routines
de sport, des todos (prendre rendez-vous) ou des choix d'alimentation. Ce lien vient en
dernier, par le semainier et ses propositions (ADR 0009), jamais par un module qui en
modifie un autre.

## Décision

Le module Santé porte quatre parties, livrées dans cet ordre : **compléments**,
**symptômes**, **analyses et mesures**, **documents et rendez-vous**. Toutes les données
sont dans le schéma `health`.

### Confidentialité

- Chaque ligne appartient à un **profil** (`profile_id`) en plus du foyer. Seul le compte
  lié à ce profil la voit ; les autres membres du foyer ne la voient pas, même par l'API.
  Seul l'utilisateur principal a un profil suivi pour l'instant.
- Les fichiers (documents, photos d'analyses) sont sur un volume dédié,
  `lifeos_health_files`, servis seulement à leur propriétaire, et **chiffrés sur le
  disque** (AES-GCM, clé dans la configuration du serveur, jamais en base). Une clé perdue
  rend les fichiers illisibles : elle se sauvegarde à part, avec les volumes.
- L'assistant agit avec les droits du compte (ADR 0011) : il lit et écrit la santé de son
  profil. Le futur semainier et l'agent de planification ne recevront qu'un résumé, défini
  à l'étape 4.

### 1. Compléments alimentaires

Un complément porte :

- un nom, une photo facultative (sinon l'initiale), des notes ;
- une **dose par jour** : une quantité et une unité (`gélule`, `comprimé`, `g`, `ml`,
  `goutte`, `dose`) ;
- un **moment** facultatif, pour l'ordre d'affichage : matin, midi, soir ou au choix ;
- un **conditionnement** : contenu (dans la même unité que la dose), prix payé et
  magasin ou site en texte libre ;
- des **liens d'achat** : un ou plusieurs, chacun avec un nom facultatif et une adresse
  `https`, ouverts depuis la page du complément ;
- une **période** : à l'année (en continu depuis une date), ou **en cure** de N semaines
  ou mois à partir d'une date, avec une pause facultative de N semaines ou mois avant de
  recommencer ;
- un état : actif ou arrêté (historique gardé).

Chaque jour, **une coche par complément** : pris ou non, quel que soit le nombre de
prises dans la journée. On peut cocher un jour passé. La page du jour liste les
compléments à prendre aujourd'hui (actifs, et en cure ce jour-là), triés par moment, avec
la série de jours pris.

**Coût par mois**, calculé à la lecture : `prix × dose × 30 / contenu`. En cure, il est
ramené à la part de jours de cure sur un cycle (cure + pause). Le total du mois additionne
les compléments actifs. Les unités ne sont jamais converties : la dose et le contenu sont
dans la même unité.

### 2. Symptômes

Une **liste de symptômes** du profil, chacun physique ou mental (mal de ventre, diarrhée,
constipation, mal de dos, mal au genou, mal à la nuque, fatigue, flou mental, syndrome
dépressif, envie de rien…), que l'utilisateur complète ; un symptôme peut être archivé.

Deux façons de noter, au choix :

- **à tout moment** : un symptôme, une intensité de 1 à 10, un jour et une heure
  facultative, une note ;
- **bilan du jour**, facultatif : les symptômes ressentis avec leur intensité, ou « rien à
  signaler », qui compte aussi comme un jour noté.

Pour l'instant c'est un journal : calendrier, historique par symptôme, filtres. La mise
en relation avec les repas, le sport ou les compléments viendra plus tard, comme signal
calculé par le semainier (ADR 0009), jamais dans ce module.

### 3. Analyses et mesures

**Marqueurs** : une bibliothèque du profil (ferritine, vitamine D, TSH…). Chacun porte un
nom, une unité, une **plage de référence** du laboratoire (min et/ou max), une **cible
personnelle** (min et/ou max, souvent plus exigeante que la plage du labo), une
**fréquence** (une fois, tous les N mois) et des **bilans** : groupes nommés (bilan
thyroïde, bilan fer) qui disent quoi demander au médecin.

Une **analyse** : une date, un laboratoire facultatif, un document facultatif (le PDF ou
la photo, partie 4), et une valeur par marqueur mesuré. Les valeurs sont saisies à la
main ou par l'assistant, qui lit le document ; l'API ne lit rien elle-même (pas d'OCR).

Lu par marqueur : la dernière valeur, sa position (sous la plage, dans la plage, dans la
cible, au-dessus), la **tendance** par rapport à la valeur précédente (mieux, pire,
stable, selon qu'elle se rapproche ou s'éloigne de la cible) et une courbe. Un marqueur
est **à refaire** quand sa fréquence est dépassée depuis sa dernière valeur ; la page
liste ceux à refaire bientôt ou en retard.

**Mesures corporelles** : poids, tour de taille, tension, etc. Un type de mesure a un nom
et une unité ; une mesure a une date, une valeur et une note. Courbe dans le temps.

### 4. Documents et rendez-vous

Un **document** : un type (ordonnance, compte rendu, résultat d'analyse, bilan — HPI par
exemple —, autre), une date, un médecin ou une spécialité en texte libre, des notes, et
un ou plusieurs fichiers (photos ou PDF, plusieurs pages). Liste filtrable par type, année
et texte. Un document de résultat peut être relié à une analyse.

Un **rendez-vous** : une date et une heure, un médecin ou une spécialité, un motif, un
lieu facultatif, des notes, et ensuite son compte rendu (un document). Le semainier les
affichera plus tard.

### Écrans

Groupe « Santé » dans le menu : **Aujourd'hui** (compléments à cocher, bilan du jour,
prochains rendez-vous, marqueurs à refaire), **Compléments** (avec coût par mois),
**Symptômes** (journal), **Analyses** (marqueurs, bilans, analyses), **Mesures**,
**Documents** et **Rendez-vous**, une page par élément et des fenêtres de modification,
selon les ADR 0015 et 0016.

### Assistant

Outils MCP pour chaque partie. Comme pour les tickets : un document envoyé dans
l'application est lu par Claude, qui propose les valeurs de l'analyse et les saisit
après confirmation. Claude peut aussi noter un symptôme (« j'ai mal au ventre, 6/10 »),
cocher un complément ou créer un rendez-vous.

## Conséquences

- Nouveau schéma `health` et nouveau volume chiffré `lifeos_health_files` ; la clé de
  chiffrement se sauvegarde à part.
- Premier module où les lignes sont filtrées par profil en plus du foyer : un test
  d'isolation couvre un second membre du même foyer.
- Hors périmètre : imports Apple Santé ou Garmin (ADR dédiée), corrélations automatiques
  (semainier), rappels et notifications (étape 6), objectifs de santé qui génèrent
  routines, todos ou repas (après le semainier).
