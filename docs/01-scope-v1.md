# LifeOS — Périmètre de la V1

## Objectif

La V1 de LifeOS est un planificateur hebdomadaire visuel.

Elle répond à une question principale :

> À quoi ressemble ma semaine en termes de repas et d'activité physique ?

La V1 doit être suffisamment petite pour être développée rapidement et suffisamment utile pour être utilisée réellement chaque semaine.

---

# Fonctionnalités incluses

## 1. Gestion du contexte hebdomadaire

Une semaine possède un contexte principal :

* semaine avec enfants ;
* semaine solo.

Le contexte peut influencer les propositions du planning.

### Semaine avec enfants

Le système doit favoriser :

* les repas rapides ;
* les repas compatibles avec les goûts des enfants ;
* les plats pouvant produire des restes ;
* les activités sportives plus courtes ou plus faciles à intégrer ;
* une organisation réduisant la préparation quotidienne.

### Semaine solo

Le système peut favoriser :

* davantage de variété ;
* des séances sportives plus longues ;
* du batch cooking ;
* des repas différents des préférences des enfants ;
* une planification plus structurée pour limiter les repas impulsifs.

---

## 2. Contexte quotidien

Chaque journée peut comporter les informations suivantes :

* télétravail ;
* travail au bureau ;
* trajet à vélo ;
* présence des enfants.

Ces informations permettent d'adapter la journée.

Exemples :

* un jour de trajet à vélo peut réduire la quantité de cardio supplémentaire ;
* une journée en télétravail peut faciliter une séance de running à midi ;
* une soirée avec les enfants peut nécessiter une séance plus courte ;
* une journée solo peut accueillir une séance plus longue.

---

## 3. Planning alimentaire hebdomadaire

L'application affiche les repas prévus pour chaque journée.

La première version gère :

* petit-déjeuner ;
* déjeuner ;
* dîner.

Une journée peut éventuellement ne pas avoir de petit-déjeuner planifié si aucun repas n'est nécessaire.

Chaque repas planifié repose sur un repas ou une recette prédéfinie.

La V1 commence avec une petite bibliothèque de repas fiables.

Objectif initial :

* 3 à 5 petits-déjeuners ;
* 10 à 15 repas principaux ;
* quelques repas plaisir planifiés.

La V1 ne cherche pas à proposer une infinité de recettes.

---

## 4. Informations nutritionnelles simples

Les repas peuvent comporter des informations nutritionnelles utiles, notamment :

* calories estimées ;
* quantité de protéines ;
* glucides ;
* lipides.

L'objectif est d'aider à construire une semaine cohérente, pas de créer un système de comptage obsessionnel.

Le système doit permettre de vérifier grossièrement :

* si les apports protéiques sont suffisants ;
* si la répartition alimentaire de la journée est cohérente ;
* si les repas sont suffisamment variés.

---

## 5. Planning sportif hebdomadaire

Chaque journée peut contenir une activité physique planifiée.

Types initiaux :

* running facile ;
* running plus long ;
* renforcement au poids du corps ;
* mobilité ;
* marche ;
* vélo domicile-travail ;
* repos actif ;
* activité libre ou familiale.

Le planning sportif doit tenir compte du contexte quotidien.

La logique initiale doit rester fondée sur des règles simples et compréhensibles.

Aucun moteur d'intelligence artificielle complexe n'est nécessaire dans la V1.

---

## 6. Vue semaine

La vue semaine est l'écran principal de LifeOS.

Elle doit permettre de voir rapidement :

* les sept jours ;
* les repas ;
* l'activité sportive ;
* le contexte du jour.

La lecture de la semaine doit prendre quelques secondes.

L'utilisateur doit pouvoir comprendre immédiatement :

* ce qui est prévu aujourd'hui ;
* ce qui est prévu demain ;
* où sont les journées les plus chargées ;
* quand sont prévues les séances sportives.

---

## 7. Vue mois

La vue mois fournit une vision globale plus légère.

Elle permet principalement de visualiser :

* l'alternance des semaines avec enfants et des semaines solo ;
* les jours de télétravail et de bureau ;
* les journées avec trajet à vélo ;
* la répartition globale des activités sportives.

La vue mois n'a pas besoin d'afficher tous les détails des repas.

---

# Fonctionnalités explicitement hors V1

Les fonctionnalités suivantes ne doivent pas être développées dans la première version.

## Stock alimentaire

Pas de gestion détaillée :

* du réfrigérateur ;
* du congélateur ;
* des placards ;
* des dates de péremption.

## Gestion avancée des courses

Pas encore de :

* génération avancée de liste de courses ;
* regroupement par magasin ;
* optimisation automatique des achats.

Une liste de courses simple pourra être étudiée après validation de l'utilité du planning.

## Suivi des prix

La V1 ne gère pas :

* les historiques de prix ;
* le prix au kilogramme par magasin ;
* les comparaisons de magasins ;
* les promotions.

## Santé et symptômes

La V1 ne gère pas :

* les douleurs abdominales ;
* le transit ;
* la rhinite ;
* l'énergie ;
* l'humeur ;
* les analyses de corrélation entre alimentation et symptômes.

Ce module reste une évolution potentielle importante, mais séparée du MVP.

## Plan sportif avancé

Pas de :

* périodisation complexe ;
* calcul automatique de charge d'entraînement ;
* synchronisation avec Garmin, Strava ou d'autres services ;
* adaptation algorithmique avancée à la récupération.

## Loisirs

La planification :

* des sorties ;
* des activités avec les enfants ;
* des projets personnels ;
* des loisirs ;

reste hors V1.

Le modèle général pourra rester compatible avec cette évolution future, sans développer ces fonctionnalités maintenant.

## Intelligence artificielle

La V1 ne nécessite aucun moteur d'IA.

La génération d'une semaine doit initialement fonctionner avec :

* des règles simples ;
* des préférences ;
* des contraintes de contexte ;
* une bibliothèque limitée de repas et d'activités.

---

# Règle d'évolution

Une nouvelle fonctionnalité est prioritaire uniquement si :

1. un besoin réel est observé pendant l'utilisation de LifeOS ;
2. le problème revient régulièrement ;
3. la fonctionnalité réduit effectivement la charge mentale ou améliore directement l'organisation.

Une idée intéressante n'est pas automatiquement une fonctionnalité à développer.

---

# Critères de validation de la V1

La V1 est considérée utile si elle permet de :

* préparer une semaine en moins de 10 minutes ;
* visualiser immédiatement les repas à venir ;
* visualiser immédiatement les activités sportives à venir ;
* adapter la semaine au contexte enfants / solo ;
* adapter les activités aux journées bureau, télétravail et vélo ;
* réduire les décisions quotidiennes improvisées.

Le premier objectif est l'usage réel.

La sophistication technique vient ensuite.
