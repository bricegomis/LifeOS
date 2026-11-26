# ADR 0021 — Accueil « Aujourd'hui » : ce qu'il reste à cocher dans la journée

Statut : accepté, 2026-11-26. Premier point de « En attendant le semainier » dans la
[roadmap](../../01-roadmap.md). Maquette validée le 2026-11-26 (canevas des maquettes,
« Aujourd'hui — ordinateur » et « Aujourd'hui — smartphone ») ; livré en code le même jour.
Applique l'[ADR 0009](0009-modular-monolith-and-planning-core.md) (l'accueil lit les modules
par leurs endpoints publiés, sans importer leur code) et l'[ADR 0016](0016-angular-material-ui-kit.md) (UI kit).

## Contexte

Sur téléphone, l'usage quotidien passe par plusieurs pages : les tâches du jour, les routines
sportives, les défis, les compléments. L'utilisateur veut un seul écran, ouvert au lancement,
avec tout ce qu'il y a à cocher dans la journée, qui servira aussi au futur widget « à faire
aujourd'hui » de l'app iPhone.

## Décision

La page **Aujourd'hui** (`/today`, l'accueil) montre d'abord **ce qu'il reste à faire**, puis
la nuit et l'énergie.

### À faire, dans cet ordre

1. **En retard** : todos ouverts dont l'échéance est passée et routines du foyer dont
   l'échéance ouverte est passée.
2. **Tâches du jour** : todos dont l'échéance est aujourd'hui et routines du foyer dues aujourd'hui.
3. **Sport** : routines sportives prévues aujourd'hui et défis du jour actifs.
4. **Compléments** : compléments actifs du jour (hors pause de cure), rangés par moment
   (Matin, Midi, Soir, Au choix).

Les todos sans échéance et ceux des jours suivants restent sur la page Tâches.

### Cocher depuis l'accueil

- Todo, routine du foyer, complément : une case. Le message qui suit propose « Annuler ».
- Routine sportive : un bouton **Démarrer** (ou **Continuer** si un passage est en cours) ouvre
  la page de la routine ; les séries se cochent là-bas, comme avant. Elle est faite quand un
  passage est terminé.
- Défi : un bouton **+ Série** ouvre une petite fenêtre avec les valeurs proposées par la page
  Sport et un champ libre. Le défi est fait quand l'objectif du jour est atteint.

Une coche appelle le même endpoint que la page du module ; l'accueil n'a ni table ni endpoint
à lui.

### Fait aujourd'hui

Ce qui est coché aujourd'hui quitte la liste et va dans **« Fait aujourd'hui (n) »**, replié.
On peut l'ouvrir et décocher : rouvrir le todo, retirer le passage du jour de la routine du
foyer, décocher le complément, retirer la dernière série du défi. Une routine sportive faite
se défait sur sa page (supprimer un passage efface ses coches).

En haut : la date, « n à faire, m faits » et une barre d'avancement de la journée.

### Le reste de la page

Inchangé ([ADR 0019](0019-garmin-import.md)) : cette nuit (sommeil, Body Battery, HRV, FC au
repos), séances du jour, objectifs de la semaine, pas et stress. Sur ordinateur, « À faire » à
gauche et ces blocs à droite ; sur téléphone, l'un sous l'autre.

## Conséquences

- La santé est privée au compte : l'accueil lit les compléments du compte connecté, comme la
  page Santé ; rien n'est exposé à un autre membre du foyer.
- Le futur widget iPhone « à faire aujourd'hui » (étape 6) affichera la même liste.
- Quand le semainier arrivera, cette page deviendra son accueil et placera ces éléments dans la journée.
