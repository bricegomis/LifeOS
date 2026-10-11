# ADR 0007 — Recettes, lignes de repas et avis du foyer

Statut : accepté. Complète l'ADR 0006 sans modifier le modèle
aliments/produits/tickets.

## Décision produit

Le vocabulaire culinaire actif est **aliment**, **recette** et **repas planifié**.
Une recette peut être une préparation élémentaire, un plat complet, un
accompagnement ou un dessert. Les usages sont exprimés par des catégories/tags,
pas par des catalogues ou types culinaires différents. Un repas planifié contient
zéro ou plusieurs recettes et préparations ponctuelles, sans composition
obligatoire par type d'aliment.

Le catalogue de repas composés et l'ancien modèle de plats/composants préassemblés
sont abandonnés. Une préparation à la volée appartient explicitement à un repas,
n'entre pas dans la bibliothèque des recettes et est persistée avec la semaine.
Elle partage la structure d'ingrédients, quantités, unités et rendement d'une
recette ; nom, durée et instructions restent facultatifs. Les duplications de
semaine copient les préparations et ne partagent aucun état mutable.

Les ingrédients d'une recette ou préparation référencent les aliments du foyer.
Un produit commercial n'est jamais choisi implicitement. Les unités et quantités
restent exactes ; aucune conversion pièce/masse n'est inventée.

## Portions et nutrition ; courses hors périmètre

Les ingrédients décrivent la préparation entière et son rendement positif.
Chaque ligne de recette ou préparation planifiée possède son propre total de
portions, ajustable par profil et fractionnaire. Le coefficient de portion d'un
profil ne fournit qu'une proposition initiale et n'impose aucune répartition aux
autres lignes du repas.

Le total de portions d'une ligne est soit explicite, soit dérivé de ses
répartitions par profil. Si des répartitions existent, elles sont la source du
total et ne sont jamais additionnées une seconde fois au total explicite.
Les quantités requises affichées par ligne et la nutrition appliquent le même ratio :
`quantité de la préparation × portions planifiées / rendement`.
Les valeurs nutritionnelles inconnues restent inconnues et rendent le total
incomplet ; les aliments comptés en pièces restent en pièces sans poids confirmé.
Les quantités ne sont pas agrégées en liste de courses dans ce périmètre.

Un repas vide est un créneau vide : aucun plat de démonstration ni recette fictive
n'est injecté. Les anciennes semaines/règles du modèle culinaire abandonné ne
sont pas converties. Il n'y a ni gestion des restes ni session de batch cooking.

## Avis du foyer

Un avis actuel modifiable et supprimable peut être associé à un aliment ou une
recette, pour une personne (`MemberProfile`) ou un groupe nommé du foyer. Il
contient une note entière facultative de 0 à 10 et/ou un commentaire facultatif ;
au moins l'un doit être renseigné. Zéro est une vraie note. Un avis commun
(notamment « Enfants ») peut coexister avec les avis individuels et n'est jamais
un profil de consommation.

Les comptes authentifiés et les profils évalués sont distincts ; les enfants
n'ont pas besoin de compte. Le compte auteur de la saisie est conservé. Les avis
sont visibles uniquement dans le foyer ; il n'y a ni historique de dégustations,
ni moyenne d'aliments pour une recette, ni score calculé, ni influence sur la
génération automatique. Les préparations ponctuelles peuvent avoir un avis
persistant dans le repas sans devenir une recette de bibliothèque.

Les relations vers aliment, recette, foyer, profil, groupe et repas sont fortes,
avec clés étrangères et unicité nécessaires ; aucun couple polymorphe
`targetType/targetId` sans intégrité référentielle. Le même aliment retrouve le
même avis partout, indépendamment de son nom ou de la recette qui l'utilise.
Les contrôles applicatifs et SQL imposent l'isolation du foyer, y compris sous
concurrence. Les avis ne sont pas des préférences alimentaires, allergies ou
interdictions.

Les avis sont consultables dans la bibliothèque et la sélection des recettes,
dans la fiche et le sélecteur d'ingrédients, et depuis le semainier. Ils guident
un choix explicite, sans modifier automatiquement les suggestions.

## Données existantes et périmètre

L'utilisateur autorise l'abandon des anciennes recettes, repas composés, semaines
et règles qui dépendent de ces concepts ; aucune transition, import, correspondance
d'IDs ou compatibilité des anciens payloads n'est maintenue. La migration EF Core
est explicite et ne réécrit aucune migration déjà appliquée. Le nettoyage des
données est limité aux tables culinaires/planning concernées dans les
environnements visés ; aucune purge globale ou action destructive de production
n'est autorisée par cette décision.

Préserver comptes/authentification, foyers, profils, aliments, produits, magasins,
prix, tickets et achats, ainsi que sport et configuration de présence hors de la
demande. Ne pas effacer globalement `localStorage` ni faire réapparaître les
anciens plats par cache ou Supabase. La source métier des recettes, avis et
semaines est l'API/PostgreSQL ; Supabase reste limité à l'authentification.

Archiver une recette déjà utilisée ou figer son contenu pour conserver les
semaines historiques ; une édition ultérieure ne réécrit pas silencieusement les
repas enregistrés. Cette conservation historique ne constitue pas un mécanisme
de compatibilité avec les anciennes semaines abandonnées.

## Exemples de conformité

- Une préparation chia-coco de 125 ml de lait et 25 g de chia pour deux portions
  donne 62,5 ml et 12,5 g pour une portion, et 250 ml et 50 g pour quatre.
- Une salade ponctuelle avec une carotte, deux tomates, une boule de mozzarella
  et 100 g de salade reste dans son repas après rechargement et duplication.
- Deux profils peuvent avoir des portions différentes de poulet et de dessert ;
  une absence de répartition n'invente pas de profil consommateur.
- Une note recette reste indépendante des notes de ses aliments ; la note 0
  reste distincte de « Non noté ».

## État d'implémentation

Le modèle de données, la migration EF Core, les endpoints API, la bibliothèque
Angular et le semainier manuel implémentent recettes, préparations ponctuelles,
portions par ligne et avis foyer-scopés. Le semainier affiche les quantités
requises par ingrédient et calcule la nutrition selon le même ratio.

Les règles, scénarios et génération automatiques de l'ancien modèle sont
supprimés et hors périmètre de cette livraison ; ils ne sont pas remplacés.
L'application n'expose pas d'agrégateur ni de liste de courses ; les quantités
affichées restent propres à chaque ligne. Aucune migration de production n'a
été exécutée.
