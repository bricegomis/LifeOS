# LifeOS — MVP semainier manuel

## Décision validée et état de livraison

Le cadrage du 7 octobre 2026 remplace le MVP centré sur la génération et le
menu équilibré. Ce document décrit la **cible approuvée**, pas des fonctionnalités
déjà livrées. Voir [roadmap](03-roadmap.md) pour les lots et leur état.
La réalisation concerne Angular / PrimeNG et l'API ASP.NET Core / PostgreSQL ;
le frontend Vue historique n'est pas refondu.

## Périmètre

- Planifier manuellement sept jours, avec plusieurs prises alimentaires et
  séances sportives par jour. Aucun événement libre, travail ou garde d'enfants.
- Calendrier desktop initial de 6 h à 20 h ; journée navigable sur mobile.
  Cette plage limite l'affichage, jamais le stockage ou l'accès aux événements.
- Ajouter, éditer, déplacer et supprimer par formulaire et clavier, sans
  dépendre du glisser-déposer. Chevauchements autorisés et lisibles.
- Trois bibliothèques administrables et persistées : produits alimentaires,
  recettes et séances sportives, isolées par foyer.
- Un repas est une recette avec portion personnelle, **ou** une liste de
  produits avec quantités et unités. Une banane est un produit, pas une recette.
  Les anciens repas composés restent lisibles et migrables sans perte.
- Séance catalogue : nom, sport, durée habituelle positive, distance optionnelle,
  intensité explicite et calories totales manuelles non négatives. L'occurrence
  reprend ces valeurs et peut les modifier sans modifier le modèle.
  Changer durée/distance ne redimensionne pas les calories.

## Nutrition personnelle et préparation

Suivre énergie, protéines, glucides et lipides de l'utilisateur seulement.
Chaque repas porte un nombre d'enfants présents, zéro compris.
Chaque enfant mange une demi-portion personnelle :

`quantités à préparer = quantités personnelles × (1 + 0,5 × nombre d'enfants)`

100 g personnels avec deux enfants donnent 200 g à préparer, mais la nutrition
reste celle des 100 g. Aucun suivi individuel enfant ni automatisation de présence.
Séparer apports alimentaires, préparation et dépense sportive ; apports moins
sport n'est pas une dépense quotidienne complète. Nutrition inconnue : afficher
un total incomplet, jamais un zéro inventé. Pas de conversion g/ml/pièces sans
information sûre.

## Conservation

API et PostgreSQL restent la source de vérité. Les semaines sont indépendantes.
Les valeurs utilisées par les événements sont préservées : éditer ou archiver
le catalogue ne réécrit pas l'historique. Remplacer le contenu est une action
explicite. Préférer l'archivage des éléments référencés.
Raccorder articles d'achat et produits nutritionnels explicitement, sans fusion
par nom, sans perdre prix ou achats. Les anciennes semaines sans horaires
restent « à positionner » jusqu'à placement manuel, sans heure inventée.
Conserver auth Supabase/JWT et isolation `household_id`.

## Reporté / exclu

Semaines types avec/sans enfant, alternance, récurrence, règles de fréquence,
génération, menu équilibré et optimisation sont hors parcours MVP. Les données
historiques sont conservées ; les anciens réglages ne doivent pas influencer
silencieusement les nouvelles semaines.
Pas de fatigue, calories sportives automatiques, connecteurs sportifs, suivi
nutritionnel enfant, nouveaux types d'événement, stock/tickets/courses automatiques,
ni refonte Vue. Une automatisation future sera guidée par l'usage réel.

## Acceptation

- Deux semaines indépendantes ; recette petit-déjeuner, banane à 16 h 30 et
  course à 17 h retrouvées après navigation et rechargement.
- Deux vélos et une autre séance le même jour.
- 100 g + deux enfants = 200 g préparés, nutrition personnelle des 100 g ;
  modifier portions/enfants ne touche que le repas concerné, zéro enfant fonctionne.
- Déplacement, édition et suppression accessibles par formulaire/clavier.
- Modification/archivage de catalogue sans perte ni réécriture historique.
- Anciennes semaines accessibles sans horaire arbitraire ; événements hors plage
  et chevauchements lisibles ; nutrition manquante et erreurs API explicites.
- Aucun recalcul automatique des repas après ajout de sport ; aucun succès
  affiché avant sauvegarde effective.
