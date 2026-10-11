# LifeOS — Semainier manuel et Aujourd'hui

Le semainier et la vue Aujourd'hui utilisent les semaines, repas et séances
persistés par l'API. Une semaine commence vide ; aucun plat de démonstration,
scénario ou menu généré ne remplace l'absence de données. Voir
[ADR 0007](architecture/decisions/0007-recipes-meal-lines-and-household-reviews.md)
pour le modèle culinaire.

## Navigation et affichage

Le semainier permet de naviguer entre semaines, revenir à aujourd'hui et aller
à une date. Chaque semaine contient sept journées indépendantes. Le calendrier
de bureau affiche les créneaux de 6 h à 20 h ; la navigation par jour et les
détails restent utilisables sur petit écran. Les événements sans heure ou hors
de cette plage restent disponibles dans les détails. Les chevauchements sont
autorisés.

Les événements sont des repas ou des séances de sport. Les repas n'ont pas de
structure obligatoire petit-déjeuner/déjeuner/dîner, ni de combinaison imposée
de protéines, féculents ou légumes. Ajouter un repas ou une séance, les
déplacer, modifier leur horaire, les dupliquer avec leur semaine ou les supprimer
s'effectue explicitement.

## Repas, recettes et portions

Un repas contient zéro ou plusieurs lignes. Chaque ligne référence une recette
de la bibliothèque ou une préparation ponctuelle propre au repas. La
préparation ponctuelle peut être créée sans nom ni instructions, avec des
aliments, quantités, unités et rendement. Elle est sauvegardée avec le repas et
copiée indépendamment lors de la duplication d'une semaine.

Les quantités d'ingrédients décrivent la préparation entière. Le nombre de
portions prévues est propre à chaque ligne ; il peut être saisi explicitement
ou dérivé de portions réparties par profil. Les coefficients de profil ne sont
que des valeurs de départ proposées par l'interface. Le total n'additionne pas
un nombre explicite aux répartitions.

La quantité à préparer est affichée pour chaque ingrédient, selon
`quantité de la préparation × portions planifiées / rendement`. Les valeurs
nutritionnelles inconnues restent inconnues et signalées. Les pièces restent
des pièces sans conversion masse inventée. Le détail affiche ces quantités
individuellement ; le semainier ne fournit pas de liste de courses agrégée.

Les recettes peuvent être consultées et modifiées depuis la bibliothèque
Recettes ; leurs ingrédients réfèrent aux aliments génériques, jamais à un
produit de marque implicite. Modifier la recette ne réécrit pas la ligne déjà
planifiée. Les avis sur aliments et recettes sont affichés lors des choix et
dans le détail ; ils ne changent pas automatiquement une sélection ou une
génération.

## Nutrition et sport

Les totaux nutritionnels des repas et de la journée sont calculés séparément
des calories sportives. Un total incomplet est accompagné d'avertissements ;
une inconnue n'est jamais remplacée par zéro. Le sport conserve les valeurs
manuelles de l'occurrence ; déplacer ou modifier sa durée ne recalcule pas ses
calories.

## États et erreurs

La page distingue le chargement, la semaine vide et les erreurs de lecture ou
des bibliothèques. L'utilisateur peut réessayer. Une modification n'est
considérée enregistrée qu'après réponse positive de l'API ; en cas d'échec, le
formulaire reste disponible et l'erreur est présentée. Les contrôles sont
actionnables au clavier, ont des libellés accessibles et ne requièrent pas le
glisser-déposer.
