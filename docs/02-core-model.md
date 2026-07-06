# LifeOS — Core Model

## Principe

Le cœur de la V0 est volontairement réduit.

```text
WeekPlan
│
└── DayPlan × 7
    │
    ├── Breakfast
    ├── Lunch
    ├── Dinner
    └── Activity
```

La V0 doit permettre de générer et modifier ce planning.

---

# WeekPlan

Représente une semaine planifiée.

## Propriétés principales

* Id
* StartDate
* Status
* Days

## Status

* Draft
* Generated
* Validated
* Archived

## Responsabilités

Un WeekPlan peut :

* être créé ;
* être généré ;
* être modifié ;
* être validé ;
* être consulté.

---

# DayPlan

Représente une journée.

## Propriétés

* Id
* Date
* Breakfast
* Lunch
* Dinner
* Activity

Le DayPlan est principalement un agrégat de lecture pour la vue hebdomadaire.

---

# MealSlot

Un MealSlot représente un repas planifié.

## Propriétés

* Id
* MealType
* MealDefinition
* EstimatedCalories
* EstimatedProteinGrams
* PreparationTimeMinutes

## MealType

* Breakfast
* Lunch
* Dinner

Un MealSlot pointe vers un repas de type :

* AssembledMeal ;
* CompositeDish.

---

# AssembledMeal

Un repas assemblé est constitué de composants indépendants.

## Exemple

```text
🥩 Steak haché
🥔 Pommes de terre
🥦 Brocolis
```

## Composants possibles

* ProteinComponent
* StarchComponent
* VegetableComponents[]
* OptionalComponent

Tous les composants ne sont pas obligatoires.

Le modèle ne doit pas imposer artificiellement :

```text
protéine + féculent + légumes
```

à tous les repas.

Il doit seulement permettre cette composition lorsque le repas s'y prête.

## Modification

Chaque composant peut être remplacé individuellement par un composant compatible.

Exemple :

```text
Steak + pommes de terre + brocolis

devient :

Steak + riz + brocolis
```

sans remplacer le reste du repas.

---

# CompositeDish

Un CompositeDish représente un plat complet.

## Exemples

* Lasagnes
* Dhal lentilles-riz
* Chili
* Curry de tempeh
* Pancakes protéinés
* Pizza maison

## Modification

Un CompositeDish est remplacé entièrement.

La V0 ne permet pas de modifier ses composants depuis le planning.

---

# MealComponent

Représente un élément utilisable dans un repas assemblé.

## Propriétés principales

* Id
* Name
* ComponentType
* EstimatedCalories
* EstimatedProteinGrams
* EstimatedCarbohydrateGrams
* EstimatedFatGrams
* DefaultPortionQuantity
* Unit
* Active

## ComponentType

Valeurs initiales :

* Protein
* Starch
* Vegetable
* Optional

L'interface peut représenter les catégories par des icônes.

Exemple :

* 🥩 protéine ;
* 🥔 féculent ;
* 🥦 légumes ;
* 🥣 complément ou sauce.

Les libellés de catégorie n'ont pas besoin d'être affichés systématiquement dans l'éditeur.

---

# CompositeDishDefinition

Décrit un plat composé réutilisable.

## Propriétés principales

* Id
* Name
* EstimatedCalories
* EstimatedProteinGrams
* EstimatedCarbohydrateGrams
* EstimatedFatGrams
* PreparationTimeMinutes
* SuitableForBreakfast
* SuitableForLunch
* SuitableForDinner
* Active

---

# Activity

Dans la V0, Activity reste volontairement très simple.

## Propriétés

* Id
* Name

## Valeurs initiales possibles

* Repos
* Marche
* Running
* Running long
* Renforcement
* Mobilité
* Vélo
* Activité familiale

Ces valeurs peuvent initialement être seedées en base ou définies en dur.

La V0 ne nécessite pas de moteur sportif avancé.

---

# Génération de semaine

Le générateur reçoit :

```text
Meal library
+
Activity list
+
Simple generation rules
```

et retourne :

```text
WeekPlan
```

La première version du moteur peut être volontairement naïve.

Objectif :

* générer tous les repas ;
* éviter quelques répétitions évidentes ;
* produire une semaine suffisamment variée ;
* associer une activité à chaque jour.

Les règles avancées liées au contexte appartiennent aux versions suivantes.

---

# Nutrition

Les repas contiennent des estimations nutritionnelles simples.

La grille principale affiche au minimum :

* calories ;
* protéines.

Les données suivantes peuvent être conservées dans le modèle :

* glucides ;
* lipides.

L'objectif initial n'est pas un tracking nutritionnel exhaustif.

---

# Principes alimentaires initiaux

La bibliothèque de départ peut favoriser les féculents suivants :

* pommes de terre ;
* riz ;
* millet ;
* quinoa ;
* pâtes 100 % sarrasin ;
* couscous de sarrasin.

Les pâtes à base de blé ne sont pas un choix par défaut.

L'avoine peut exister dans la bibliothèque, mais ne doit pas être utilisée comme base automatique avant validation de sa bonne tolérance.

Les repas principaux peuvent privilégier la présence de légumes, tout en conservant une règle souple.

---

# Évolutions prévues du modèle

## V1

Ajout possible :

* WeekContext
* DayContext
* KidsWeek / SoloWeek
* WorkLocation
* BikeCommute
* AvailableSportDuration

## V2

Ajout possible :

* IsLocked sur MealSlot ;
* verrouillage d'un MealComponent ;
* génération partielle ;
* gestion des portions ;
* quantités par personne ;
* restes et préparation multiple.

## Plus tard

Entités candidates :

* Ingredient
* Store
* PriceHistory
* InventoryItem
* ShoppingList
* SymptomLog
* HealthMetric
* CalendarEvent

Ces concepts ne doivent pas être ajoutés avant qu'une fonctionnalité réelle les rende nécessaires.
