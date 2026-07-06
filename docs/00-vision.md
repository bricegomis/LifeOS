# FILE: docs/00-vision.md

# LifeOS — Vision

## Problème

Mon quotidien alterne entre plusieurs contextes qui changent fortement mon organisation :

* semaine avec mes enfants ;
* semaine sans enfants ;
* télétravail ;
* travail au bureau ;
* déplacements domicile-travail à vélo ;
* jours où j'ai plus ou moins de temps disponible.

La planification de mes repas et de mes activités sportives me demande trop de décisions répétitives.

Lorsque je suis fatigué ou désorganisé, l'absence de plan augmente le risque :

* de manger n'importe quoi ;
* d'acheter des aliments inutilement chers ;
* de faire des courses improvisées ;
* de commander ou préparer un repas par défaut qui ne correspond pas à mes objectifs ;
* de ne pas faire de sport faute d'avoir décidé quoi faire ;
* de perdre du temps et de l'énergie mentale à prendre quotidiennement les mêmes décisions.

## Vision

LifeOS est un assistant personnel de planification.

Sa première mission est simple :

> Préparer et visualiser ma semaine afin de réduire au maximum les décisions quotidiennes liées aux repas et à l'activité physique.

LifeOS ne cherche pas à être immédiatement un outil de suivi exhaustif de la vie quotidienne.

Il doit d'abord résoudre un problème concret :

> Que vais-je manger et quelle activité physique vais-je faire aujourd'hui et cette semaine ?

## Principe central

La semaine est l'objet central de la première version.

Le fonctionnement doit être semi-automatique :

1. le système connaît ou reçoit le contexte de la semaine ;
2. il génère une proposition complète ;
3. l'utilisateur visualise la semaine ;
4. il modifie rapidement ce qui ne lui convient pas ;
5. il valide son planning ;
6. il suit ensuite le plan sans avoir à prendre les mêmes décisions chaque jour.

## Philosophie

### Réduire la charge mentale

LifeOS doit proposer des choix utiles plutôt que multiplier les possibilités.

L'objectif n'est pas d'avoir une bibliothèque de milliers de recettes.

L'objectif est de disposer d'un ensemble raisonnable de repas fiables et variés, puis de les organiser intelligemment.

### Garder le contrôle

Le système propose.

L'utilisateur décide.

La génération doit accélérer la planification sans transformer l'application en boîte noire.

### Être visuel

Le planning de la semaine est l'interface principale.

Une consultation rapide doit permettre de comprendre :

* les repas de la journée ;
* les repas des jours suivants ;
* l'activité physique prévue.

La planification détaillée se fera principalement sur ordinateur.

Sur smartphone, l'usage principal sera la consultation quotidienne rapide.

### Rester évolutif sans anticiper inutilement

LifeOS pourra évoluer vers :

* la gestion des courses ;
* le budget alimentaire ;
* les prix par magasin ;
* les stocks ;
* le suivi digestif ;
* le suivi des symptômes ;
* la planification de loisirs ;
* l'intégration avec des calendriers externes.

Ces possibilités futures ne doivent pas compliquer la V0.

## Définition du succès

LifeOS est utile si je peux :

* préparer ma semaine rapidement ;
* voir immédiatement ce que je vais manger ;
* voir l'activité prévue chaque jour ;
* limiter les décisions alimentaires improvisées ;
* éviter une partie des écarts liés à la fatigue ;
* faire mes courses avec une idée claire de la semaine à venir.

L'objectif à terme est de pouvoir dire :

> J'organise ma semaine une fois, puis je n'ai presque plus besoin de réfléchir à mes repas et à mon sport pendant plusieurs jours.

# FILE: docs/01-scope-and-versions.md

# LifeOS — Périmètre et versions

## Principe de développement

LifeOS doit être construit progressivement.

Une fonctionnalité n'est prioritaire que si elle répond à un problème concret et récurrent.

Le projet doit rester suffisamment petit pour être utilisé rapidement.

---

# V0 — Weekly Planner

## Objectif

Créer une première application utilisable permettant de générer, modifier et consulter une semaine complète.

La V0 doit répondre à deux questions :

> Qu'est-ce que je mange cette semaine ?

et :

> Quelle activité physique est prévue chaque jour ?

## Fonctionnalités V0

### Planning hebdomadaire

Une grille contient :

* une ligne par jour ;
* une colonne Petit-déjeuner ;
* une colonne Déjeuner ;
* une colonne Dîner ;
* une colonne Activité physique.

Exemple :

| Jour  | Petit-déjeuner | Déjeuner                 | Dîner             | Activité |
| ----- | -------------- | ------------------------ | ----------------- | -------- |
| Lundi | Œufs + fruit   | Steak + PdT + légumes    | Curry de tempeh   | Marche   |
| Mardi | Shaker         | Sardines + riz + légumes | Pâtes de sarrasin | Running  |

### Génération semi-automatique

L'application génère une proposition complète de 7 jours :

* 7 petits-déjeuners ;
* 7 déjeuners ;
* 7 dîners ;
* 7 activités physiques.

L'utilisateur peut ensuite modifier les propositions.

### Deux types de repas

#### Repas assemblé

Un repas assemblé est composé de composants interchangeables.

Exemple :

* protéine ;
* féculent ;
* légumes ;
* accompagnement ou sauce éventuelle.

Exemple concret :

* steak ;
* pommes de terre ;
* brocolis.

Chaque composant peut être remplacé individuellement.

#### Plat composé

Un plat composé est considéré comme une unité.

Exemples :

* lasagnes ;
* dhal de lentilles et riz ;
* chili ;
* curry ;
* pancakes protéinés.

Un plat composé est remplacé entièrement.

### Informations affichées

Une cellule de repas affiche :

* le repas ou ses composants ;
* les calories estimées ;
* les protéines estimées ;
* le temps de préparation.

Les autres informations nutritionnelles peuvent être disponibles dans une vue détaillée, mais elles ne sont pas nécessaires dans la grille principale.

### Activité physique

La V0 utilise une liste simple d'activités définies en dur.

Exemples :

* Repos ;
* Marche ;
* Running ;
* Running long ;
* Renforcement ;
* Mobilité ;
* Vélo ;
* Activité familiale.

Aucune logique sportive avancée n'est nécessaire dans la première version.

### Desktop

La planification se fait principalement sur ordinateur.

La vue principale est une grille hebdomadaire.

Un clic sur une cellule de repas ouvre un drawer latéral d'édition.

### Mobile

Le smartphone est principalement destiné à la consultation quotidienne.

La grille peut utiliser un scroll horizontal.

L'édition d'un repas ouvre une popup, un bottom sheet ou une vue plein écran selon l'espace disponible.

---

# V1 — Planning contextuel

La V1 ajoutera le contexte autour du planning.

Exemples :

* semaine avec enfants ;
* semaine solo ;
* télétravail ;
* travail au bureau ;
* déplacement à vélo ;
* temps disponible.

Ces éléments serviront progressivement à améliorer les propositions de repas et d'activités.

La génération pourra alors appliquer des règles comme :

* repas plus rapides les semaines avec enfants ;
* séances plus courtes les jours chargés ;
* moins de cardio les jours avec trajet vélo ;
* running plus facile à placer les jours de télétravail ;
* davantage de batch cooking pendant certaines périodes.

---

# V2 — Planification enrichie

Fonctionnalités candidates :

* verrouillage d'un repas ;
* verrouillage d'un composant ;
* régénération partielle ;
* régénération d'une journée ;
* régénération uniquement des repas ;
* régénération uniquement du sport ;
* calcul des quantités à préparer ;
* gestion des portions ;
* prise en compte plus fine de la semaine avec enfants.

---

# Versions futures possibles

## Courses

* génération d'une liste de courses ;
* regroupement des ingrédients ;
* estimation des quantités ;
* export mobile.

## Prix et magasins

* historique des prix ;
* prix au kilogramme ;
* comparaison entre magasins ;
* coût par repas ;
* coût hebdomadaire.

## Stock

* réfrigérateur ;
* congélateur ;
* placards ;
* gestion des restes ;
* utilisation prioritaire du stock.

## Santé

* douleur digestive ;
* transit ;
* fatigue ;
* humeur ;
* rhinite ;
* commentaires libres ;
* analyse de tendances.

## Loisirs et vie personnelle

À terme, LifeOS pourra éventuellement afficher ou planifier :

* activités avec les enfants ;
* sorties ;
* loisirs ;
* projets personnels.

Cette évolution ne doit être envisagée qu'après validation réelle des modules alimentation et sport.

## Calendriers externes

Objectifs possibles :

* exporter les repas dans Outlook ;
* exporter les séances sportives ;
* afficher les événements externes autour du planning ;
* utiliser les contraintes du calendrier comme contexte de génération.

L'intégration bidirectionnelle avec Outlook est une évolution future et ne fait pas partie de la V0.
