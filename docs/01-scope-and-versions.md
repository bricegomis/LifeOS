# LifeOS — Cadrage fonctionnel du MVP

## Finalité du MVP

Le MVP permet de préparer manuellement une semaine de repas flexible, puis de
demander des scénarios révisables avant leur application. Il couvre tous les
repas du foyer : petit-déjeuner, déjeuner, dîner, collations facultatives et
plusieurs collations dans une même journée.

Le planning prend aussi en compte les repas rapides et les restes. Les
suggestions sont lancées à la demande, classées sans recommandation par défaut
(par exemple : équilibre nutritionnel, économie, réduction du gaspillage).

## Contexte du foyer et de la semaine

- La présence de chaque enfant est saisie manuellement pour chaque semaine.
  Les portions sont recalculées à partir des membres présents.
- Le télétravail ou le bureau est renseigné au jour le jour ; les journées au
  bureau favorisent des repas transportables.
- Les séances sportives et trajets vélo sont saisis chaque semaine avec leur
  type, intensité et durée. Les estimations de dépense sont configurables par
  type et intensité.
- Un changement de séance pendant la semaine ne modifie pas automatiquement
  les repas : LifeOS actualise et signale uniquement l'effet sur la balance
  énergétique et le déficit.

## Bibliothèque alimentaire et repas

La bibliothèque contrôlée est créée manuellement. Elle contient :

- des recettes simples ou complètes ;
- des composants réutilisables (protéine, légumes rôtis, féculents, etc.) ;
- des repas composés qui assemblent ces composants.

Un repas planifié peut assembler plusieurs recettes et composants. Chaque
recette comporte des ingrédients, des étapes, un nombre de portions de
référence identiques, une durée, des métadonnées structurées et des tags.

Les propriétés calculables restent structurées. Les tags servent aux filtres
souples — par exemple `transportable`, `froid`, `réchauffable`, `express`,
`végétarien` ou `randonnée` — et leur catalogue initial peut être étendu par
chaque foyer. Les profils du foyer définissent un coefficient de portion
habituel ajustable par repas.

Les composants de batch cooking produisent des portions à répartir sur plusieurs
repas, sans suivi détaillé du réfrigérateur ou du congélateur dans le MVP. Les
repas récurrents simples (shaker, sardines avec restes, oeufs durs, collation)
sont des recettes ordinaires.

## Nutrition

Open Food Facts est utilisé pour rechercher un produit par texte ou code-barres
saisi manuellement. Le scan par caméra est différé. Les données sont conservées
localement et restent corrigeables ; les produits bruts ou introuvables peuvent
être créés comme articles génériques avec macros et unité.

Le MVP calcule calories, protéines, glucides et lipides. Le suivi personnel peut
être précis face à des cibles ; les valeurs enfants sont informatives et ne
portent pas de cible médicale. L'utilisateur saisit dépense quotidienne de base,
déficit net cible et objectifs de macros. Les protéines sont fixes, les glucides
sont adaptés prioritairement aux jours d'effort et les lipides restent dans une
plage cible.

La cible alimentaire est :

`dépense de base + dépenses des séances - déficit net cible`

LifeOS affiche les hypothèses et avertissements nécessaires, sans prétention de
précision médicale. Le suivi du poids et l'ajustement automatique sont hors MVP.

## Courses, stock et budget

Le stock est saisi manuellement, avec des unités adaptées (g, ml, pièce,
paquet, etc.) et un nombre limité de conversions sûres. La liste de courses est
consolidée depuis le menu puis déduite du stock.

Sur téléphone, le MVP permet de consulter et cocher la liste de courses. Le
budget est une information de comparaison : il n'impose ni plafond ni alerte.
Les prix par magasin et leur historique sont prévus en V2, avec saisie manuelle
et import de tickets ultérieur. Magasin favori et alternatives ne seront
proposés que lorsque l'économie est significative.

La mutualisation des ingrédients et leur réutilisation sont prioritaires dans les
suggestions. La répétition des repas est laissée au choix de l'utilisateur et
signalée, jamais arbitrairement bloquée.

## Suggestions et historique

Le moteur de suggestion est déterministe, transparent et testable. L'IA est une
couche d'assistance ultérieure. Le moteur peut classer les recettes, assembler
les composants existants et ajuster les portions en tenant compte du temps, des
ingrédients et stocks, des préférences souples, du budget informatif, des
macros et du sport. Il n'applique aucune modification silencieuse et ne génère
pas de nouvelles recettes par IA dans le MVP.

Les semaines passées sont conservées, consultables et duplicables, sans
statistiques avancées. Chaque repas peut être marqué prévu, consommé, remplacé
ou ignoré ; une consommation non planifiée peut être ajoutée en recherchant une
recette ou un article. Le stock n'est pas mis à jour automatiquement.

## Hors périmètre explicite du MVP

- allergies, contraintes médicales et recommandations nutritionnelles médicales ;
- notifications ;
- créneaux de cuisine et modèle d'équipement ;
- scan caméra des codes-barres ;
- prix par magasin, historique de prix et import de tickets ;
- gestion avancée du frigo, congélateur et péremptions ;
- statistiques avancées, suivi du poids et ajustement nutritionnel automatique ;
- génération de recettes par IA ;
- collaboration adulte et invitations.
