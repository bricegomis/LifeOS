# LifeOS — Vision produit

Révisée le 2026-11-11 avec
[ADR 0009](architecture/decisions/0009-modular-monolith-and-planning-core.md).
La version précédente limitait LifeOS aux repas et au sport.

## Problème à résoudre

Tenir un foyer monoparental avec deux enfants demande de décider en permanence :
quoi manger, quand s'entraîner, quoi faire et quand, quel suivi de santé assurer.
Ces décisions dépendent les unes des autres et reviennent chaque semaine. Leur
accumulation crée une charge mentale évitable.

## Vision

LifeOS décide à l'avance, avec l'accord de l'utilisateur, pour qu'il n'ait plus
qu'à faire et à profiter du moment présent.

Le cœur du produit est le **semainier** : un calendrier, à la manière d'Outlook ou
de Google Agenda, qui montre tout ce qui est prévu — repas, séances de sport,
choses à faire, rendez-vous et prises liés à la santé — autour des créneaux déjà
occupés de l'agenda personnel.

LifeOS prépare ce planning et le tient à jour. Si une séance est faite ou manquée,
il signale que les repas suivants ne conviennent plus et propose de les ajuster.

## Domaines

- **Alimentation** : aliments, recettes, prix, repas du foyer.
- **Sport** : séances types, récurrence, séances réalisées, dépense calorique.
- **Choses à faire** : routines du foyer, todos ponctuels, projets.
- **Santé** : mesures, symptômes quotidiens, rendez-vous, traitements,
  ordonnances, résultats d'analyse.
- D'autres domaines pourront s'ajouter sans remettre en cause les précédents.

## Utilisateur et foyer

Un compte propriétaire par foyer, dans le contexte d'un parent solo avec deux
enfants. Les repas sont planifiés pour tout le foyer. Le sport, les tâches et la
santé concernent d'abord l'utilisateur principal. Les données métier sont toujours
rattachées à un foyer isolé ; les données de santé sont en plus privées par personne.

Le produit doit pouvoir accueillir plus tard d'autres personnes suivies et d'autres
adultes connectés, sans changer de modèle.

## Principes produit

### LifeOS propose, l'utilisateur valide

Rien ne change dans le planning sans action ou accord de l'utilisateur. Chaque
proposition explique ses raisons et ses effets. Quand un type de proposition est
toujours accepté, il pourra être automatisé, explicitement et de façon réversible.

### Décider le moins possible

Le planning arrive déjà rempli. Les horaires, portions et quantités ont des
valeurs par défaut. Valider une semaine doit prendre quelques minutes.

### Explicable et honnête

Une valeur inconnue reste inconnue : pas de nutrition, de conversion ni de
dépense inventée. Un calcul incomplet est signalé comme tel.

### Pragmatique

Les données sont saisies et corrigées à la main quand cela rend le résultat plus
fiable. Les imports automatiques (agenda, montre, documents) viennent ensuite.

### Modulaire

Chaque domaine fonctionne seul et reste utile sans les autres. Les liens entre
domaines passent par le semainier.

## Usage

L'ordinateur sert à construire et à réviser le planning. Le téléphone sert à
consulter la journée et à cocher ce qui est fait, y compris sans réseau.

## Hors périmètre

Stock, liste de courses, enrichissement automatique depuis des bases externes.
LifeOS ne remplace pas l'agenda personnel : il le lit.

## Mesure de succès

Le produit est utile si la semaine est prête sans effort, si l'utilisateur sait
chaque jour quoi faire sans avoir à y réfléchir, et si les imprévus sont absorbés
par une proposition qu'il lui suffit d'accepter.
