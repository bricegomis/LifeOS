# ADR 0014 — Sport : routines quotidiennes et bibliothèque d'exercices

Statut : accepté et livré en code, 2026-11-16. Cadrage d'un lot ajouté à l'étape 2 de la
[roadmap](../../01-roadmap.md). Maquette validée le 2026-11-16 (canevas des maquettes,
« Sport — routines (smartphone) »). Étend l'[ADR 0013](0013-sport-module.md) (module Sport) sans le modifier : séances,
objectifs et défis restent tels quels.

## Contexte

Besoin exprimé le 2026-11-16 : suivre des exercices à faire tous les jours pour
renforcer les tendons, éviter les blessures, remuscler le dos et les épaules, et
tout ce qu'un kiné peut prescrire. Exemple : tenir la chaise x secondes avec un
poids de n kg, et voir les secondes et le poids évoluer.

Les défis du jour ne conviennent pas :

- un défi additionne un volume (« +20 pompes »), alors qu'ici on suit la progression
  d'une tenue ou d'une charge ;
- un défi n'a qu'un nombre (`reps` ou `minutes`), sans secondes ni charge ;
- un défi n'a ni image ni consigne ;
- un défi par exercice donnerait autant de séries, de jokers et de cartes à
  l'accueil, alors que la question du jour est « ai-je fait ma routine ».

## Décision

Le module Sport gagne deux notions : les **exercices** et les **routines**.

### Exercices

Un exercice est une fiche de la bibliothèque du foyer, comme une séance type. Il
porte :

- un nom ;
- une image facultative, aux mêmes règles que les images d'aliments ;
- une consigne facultative en texte libre (position, points d'attention) ;
- une mesure : `hold` (tenue, en secondes) ou `reps` (répétitions) ;
- « avec charge » ou non ; la charge se saisit en kg, par pas de 0,5.

La mesure d'un exercice ne change plus une fois qu'il a été réalisé, pour que son
historique reste comparable. Un exercice peut être archivé ; son historique reste.

### Routines

Une routine appartient à un profil. Elle porte un nom, ses jours de la semaine
(tous par défaut), un nombre de jokers par semaine (0 à 2) et une liste ordonnée
d'exercices. Plusieurs routines peuvent tourner en même temps (« Tendons »,
« Dos et épaules ») ; chacune a sa série. Un même exercice peut figurer dans
plusieurs routines.

Chaque ligne de routine fixe la cible du moment pour un exercice : un nombre de
séries (1 par défaut), une valeur par série (secondes ou répétitions) et, si
l'exercice est avec charge, un poids. Exemple : « Chaise, 2 × 45 s à 10 kg ».

- **La cible se règle à la main.** L'application ne propose pas de l'augmenter.
- Une cible, une liste d'exercices ou des jours modifiés valent à partir du jour
  même ; les jours passés restent jugés avec la routine d'alors.
- Une routine peut être arrêtée ; son historique reste.

### Réalisation du jour

Une ligne par exercice et par jour, pas de saisie série par série.

- **Cocher** un exercice l'enregistre avec les valeurs de la cible. La valeur et
  la charge réellement faites sont modifiables (« 60 s à 12 kg ») ; le nombre de
  séries n'est pas ressaisi.
- Décocher supprime la réalisation. Un jour passé prévu peut être complété ou corrigé
  par l'API et l'assistant ; l'écran ne coche que le jour même. Un jour non prévu
  ne se coche pas.
- **La routine est faite quand tous ses exercices du jour sont cochés.** Une valeur
  inférieure à la cible compte quand même : c'est la coche qui valide, les chiffres
  servent au suivi.
- Pas de calories ; la routine n'est pas une séance et ne compte pas dans les
  objectifs de la semaine.

### Suivi, calculé à la lecture

- **Série par routine** : nombre de jours prévus d'affilée où la routine est faite,
  avec le record. Les jours non prévus ne comptent pas et ne cassent rien. Un jour
  prévu manqué est couvert par un joker s'il en reste dans sa semaine, comme pour
  les défis ; le jour en cours ne casse jamais la série avant minuit.
- **Calendrier par routine** : une case par jour, colorée selon la part d'exercices
  cochés (0, moins de 50 %, moins de 100 %, faite, joker), grisée les jours non prévus.
- **Progression par exercice** : courbe de la valeur et de la charge dans le temps,
  toutes routines confondues, avec le record de valeur et le record de charge. Rien
  n'est combiné : pas de score « secondes × kg ».
- Pas de totaux cumulés ni de paliers.

### Écrans

- **Accueil Sport** : une carte par routine prévue aujourd'hui (« Tendons, 3 / 6 »,
  série en cours), au-dessus des défis.
- **Faire la routine** : les exercices dans l'ordre, chacun avec son image, sa
  consigne, sa cible et sa coche ; un appui sur les valeurs les modifie. Pensé pour
  le téléphone, tenu d'une main.
- **Progression** : par routine, calendrier, série et record ; par exercice, courbe
  et records.
- **Réglages du sport** : exercices et routines, modifiables aussi sur téléphone
  (photo de la fiche du kiné).

### Assistant

Outils MCP sur les mêmes cas d'usage : créer et modifier un exercice (image par
URL) et une routine, cocher un exercice (« j'ai fait la chaise 60 s à 12 kg »),
lire l'état du jour et le bilan d'une routine ou d'un exercice.

### Contrat publié

S'ajoute au contrat du module : état des routines du jour par profil (prévue,
exercices faits sur prévus).

## Hors périmètre

- Saisie série par série, temps de repos, chronomètre intégré.
- Progression automatique ou suggérée de la cible.
- Programmes à durée fixe (« 6 semaines, puis phase 2 »).
- Rappels et notifications (étape 6).
- Diagnostic, douleur ou suivi médical : ils relèvent du module Santé, propre au
  profil. La consigne d'un exercice reste une donnée de sport.

## Conséquences

- Nouvelles tables dans le schéma `sports` (migration `SportRoutines`, additive) :
  exercices, routines, versions de routine (jours, jokers et lignes à partir d'un
  jour), réalisations. Toutes filtrées par
  foyer, avec des références composites vers le foyer et le profil.
- Les images d'exercices vont sur un nouveau volume durable, `lifeos_sport_images`
  (`/data/sports`), déclaré dans les deux fichiers compose et à ajouter aux
  sauvegardes. Le stockage d'images (contrôle du format, téléchargement par URL)
  sort du module Alimentation pour être partagé par les modules.
- Un exercice utilisé par une routine en cours ne s'archive pas ; il faut d'abord
  le retirer de la routine.
- Le calcul de série, de jokers et de calendrier des défis est repris pour les
  routines, avec en plus les jours non prévus.
- Les défis ne changent pas : ils restent l'outil du volume cumulé.
