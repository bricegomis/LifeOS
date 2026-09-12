# LifeOS — Concepts fonctionnels

Ce document décrit le vocabulaire produit validé. Il ne prescrit ni architecture
technique, ni stockage, ni modèle de base de données ; ces sujets seront évalués
pendant l'audit technique.

## Foyer et profils

Un **foyer** est le périmètre isolé qui possède toutes les données métier. Il
dispose d'un propriétaire dans le MVP et contient des profils de consommation.
Un profil porte notamment un coefficient de portion habituel, ajustable selon le
repas et les membres présents.

## Articles, recettes et composants

Un **article** est un produit achetable ou consommable, avec une unité et, si
connues, ses données nutritionnelles corrigibles. Il peut provenir d'Open Food
Facts ou être créé manuellement.

Une **recette** définit ingrédients, étapes, portions de référence, durée,
macros, métadonnées structurées et tags. Les recettes simples sont représentées
comme les autres recettes.

Un **composant** est une recette ou préparation réutilisable dans plusieurs
repas, par exemple une protéine préparée, des légumes rôtis ou un féculent. Un
composant de batch cooking rend disponibles plusieurs portions distribuables
dans les repas planifiés.

Un **repas composé** associe une ou plusieurs recettes et composants. Il permet
de représenter un repas planifié sans contraindre tous les repas à une structure
fixe.

## Semaine et repas planifiés

Une **semaine** contient les journées et reste consultable après son passage.
Elle peut être dupliquée. Une journée contient les créneaux petit-déjeuner,
déjeuner, dîner et zéro ou plusieurs collations.

Un **repas planifié** référence les recettes et composants qui le composent,
leurs portions calculées et son statut : prévu, consommé, remplacé ou ignoré.
Une consommation non planifiée est également un repas consigné, recherché dans
la bibliothèque ou les articles.

Le **contexte hebdomadaire** rassemble les présences des enfants, le statut
télétravail/bureau quotidien et le sport déclaré. Il sert de donnée explicite
aux calculs et suggestions.

## Sport et nutrition

Une **séance** ou un **trajet vélo** a un type, une intensité, une durée et une
estimation de dépense paramétrable. Les calculs de nutrition utilisent les
macros et la cible alimentaire définie par l'utilisateur ; ils exposent leurs
hypothèses plutôt que de se présenter comme un avis médical.

## Stock, courses et scénarios

Un **stock** est une quantité manuelle d'article dans une unité compatible. Les
conversions ne sont admises que lorsqu'elles sont sûres.

Une **liste de courses** consolide les besoins du menu et soustrait le stock
déclaré. Elle est consultable et cochable sur mobile.

Un **scénario** est une proposition de semaine classée selon un objectif
explicite. Il explique ses compromis et demeure un brouillon jusqu'à ce que
l'utilisateur le modifie ou l'applique.
