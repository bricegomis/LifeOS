# LifeOS — UX du semainier manuel (cible approuvée)

## Navigation et affichage

Ouvrir directement la semaine datée, sans configuration ni génération.
Naviguer semaine précédente/suivante et revenir à aujourd'hui. Chaque semaine
est indépendante. Desktop : sept jours et axe horaire 6 h–20 h.
Mobile : une journée lisible, navigation entre les sept dates.
Les événements hors plage et ceux « à positionner » restent accessibles dans
des sections dédiées ; aucun horaire historique n'est inventé.
Les chevauchements sont autorisés et les cartes restent lisibles.

## Actions

Ajouter depuis un créneau ou un bouton explicite. Choisir **Alimentation**
ou **Sport**, puis rechercher un contenu dans les bibliothèques.
Alimentation : recette et portion personnelle, ou liste de produits avec
quantités et unités ; nombre d'enfants présents par repas, zéro compris.
Sport : modèle puis durée, distance optionnelle, intensité et calories totales
manuelles ajustables pour cette seule occurrence.
Indiquer que modifier durée/distance ne recalcule pas les calories.

Un formulaire permet toujours d'éditer date/début/fin, déplacer et supprimer ;
aucune action n'exige le drag-and-drop. Étiquettes, focus, clavier et cibles
tactiles adaptées sont requis. Le remplacement du contenu est explicite.
Un modèle archivé n'est pas proposé pour un nouvel événement mais les anciens
événements conservent leurs valeurs.

## Résumés et états

Afficher séparément apports personnels (kcal, protéines, glucides, lipides),
quantités à préparer et dépense sportive déclarée. Ne jamais majorer les macros
personnelles avec les enfants ni appeler apports moins sport « dépense totale ».
Nutrition/conversion inconnue : total incomplet et raison visible.
La vue Aujourd'hui emploie les mêmes événements et règles de calcul.

Prévoir chargement, semaine vide et échec API/sauvegarde avec possibilité de
réessayer. Garder le formulaire en cas d'erreur ; ne pas afficher un succès
ou modifier le calendrier avant sauvegarde effective.
Retirer alternance, modèles kids/solo, règles, génération et menu équilibré
du parcours, tout en conservant les données historiques.
