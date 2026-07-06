# LifeOS — UX du Weekly Planner V0

## Objectif de l'écran

La vue Weekly Planner est l'écran principal de LifeOS V0.

Elle doit permettre de répondre en quelques secondes à :

* Qu'est-ce que je mange aujourd'hui ?
* Qu'est-ce que je mange demain ?
* Quelle activité physique est prévue ?
* Est-ce que l'ensemble de ma semaine me convient ?

La planification détaillée est principalement pensée pour ordinateur.

L'usage mobile est principalement orienté consultation quotidienne.

---

# Structure desktop

## En-tête

L'en-tête contient :

* navigation semaine précédente ;
* période affichée ;
* navigation semaine suivante ;
* bouton de génération de la semaine.

Exemple :

```text
←    Semaine du 6 au 12 juillet    →       [ Générer la semaine ]
```

La V0 doit rester sobre.

Les informations de contexte comme semaine avec enfants ou semaine solo appartiennent à une version ultérieure.

---

# Grille principale

La grille comporte :

* une ligne par jour ;
* une colonne Jour ;
* une colonne Petit-déjeuner ;
* une colonne Déjeuner ;
* une colonne Dîner ;
* une colonne Activité physique.

Exemple :

```text
┌──────────┬──────────────────┬──────────────────┬──────────────────┬────────────────┐
│ Jour     │ Petit-déjeuner   │ Déjeuner         │ Dîner            │ Activité       │
├──────────┼──────────────────┼──────────────────┼──────────────────┼────────────────┤
│ Lundi    │ Œufs + fruit     │ Steak            │ Curry tempeh     │ Marche         │
│          │                  │ PdT               │                  │ 45 min         │
│          │ 410 kcal         │ Brocolis         │ 620 kcal         │                │
│          │ 31 g prot.       │                  │ 35 g prot.       │                │
│          │ ⏱ 8 min         │ 650 kcal         │ ⏱ 20 min        │                │
│          │                  │ 48 g prot.       │                  │                │
│          │                  │ ⏱ 15 min        │                  │                │
└──────────┴──────────────────┴──────────────────┴──────────────────┴────────────────┘
```

---

# Contenu d'une cellule de repas

## Repas assemblé

Un repas assemblé affiche directement ses composants.

Exemple :

```text
🥩 Steak
🥔 Pommes de terre
🥦 Brocolis

650 kcal · 48 g prot.
⏱ 15 min
```

Les icônes suffisent à distinguer les catégories.

Il n'est pas nécessaire d'afficher les titres :

* Protéine ;
* Féculent ;
* Légumes.

La cellule doit rester compacte.

## Plat composé

Un plat composé affiche simplement son nom.

Exemple :

```text
Dhal lentilles-riz

620 kcal · 32 g prot.
⏱ 25 min
```

ou :

```text
Lasagnes

730 kcal · 38 g prot.
⏱ 45 min
```

---

# Interaction principale

Un clic sur une cellule ouvre l'éditeur du repas.

Il n'y a pas de bouton « Modifier » visible en permanence dans chaque cellule.

La cellule elle-même est interactive.

Le curseur et les états hover doivent rendre cette interaction compréhensible sur desktop.

---

# Éditeur desktop

Sur ordinateur, l'édition ouvre un drawer latéral.

## Repas assemblé

Exemple :

```text
Déjeuner — Mardi

🥩  Steak haché                 >
🥔  Pommes de terre             >
🥦  Brocolis                    >
🥣  Sauce yaourt                >

620 kcal · 45 g prot.
⏱ 15 min

[ Valider ]
```

Chaque ligne est interactive.

Un clic sur un composant ouvre la bibliothèque complète des alternatives compatibles.

Exemple pour un féculent :

```text
Rechercher...

Pommes de terre
Riz
Millet
Quinoa
Pâtes 100 % sarrasin
Couscous de sarrasin
```

La bibliothèque complète est disponible.

Des filtres ou une recherche pourront être ajoutés si le volume le justifie.

## Plat composé

Exemple :

```text
Dîner — Mardi

Dhal lentilles-riz             >

620 kcal · 32 g prot.
⏱ 25 min

[ Valider ]
```

Un clic sur le plat ouvre la bibliothèque des plats compatibles avec ce type de repas.

Le plat est remplacé entièrement.

---

# Éditeur mobile

Sur smartphone, l'édition utilise :

* une popup ;
* un bottom sheet ;
* ou un écran plein format si nécessaire.

Le contenu fonctionnel reste le même que sur desktop.

L'interface mobile ne doit pas reproduire un drawer desktop réduit artificiellement.

---

# Consultation mobile

La grille hebdomadaire reste accessible sur smartphone.

Elle peut utiliser un scroll horizontal.

L'objectif principal sur mobile est cependant la consultation de la journée en cours.

La vue doit permettre de retrouver rapidement :

* petit-déjeuner ;
* déjeuner ;
* dîner ;
* activité.

La planification complète est principalement destinée au desktop.

---

# Colonne Activité

Dans la V0, la colonne Activité utilise une liste simple.

Exemples :

* Repos
* Marche
* Running
* Running long
* Renforcement
* Mobilité
* Vélo
* Activité familiale

L'édition peut utiliser un simple sélecteur.

La V0 ne nécessite pas encore :

* de détail d'exercices ;
* de séries ;
* de répétitions ;
* de charge d'entraînement ;
* d'intégration Garmin ou Strava.

---

# Génération

Le bouton principal :

```text
[ Générer la semaine ]
```

génère :

* les 21 créneaux alimentaires ;
* les 7 activités.

Dans la V0, le comportement peut rester simple.

Le système peut :

* utiliser la bibliothèque existante ;
* éviter certaines répétitions excessives ;
* répartir grossièrement les différentes sources de protéines ;
* varier les féculents ;
* proposer des activités variées.

---

# Ce qui n'est pas dans la V0

## Verrouillage

Le verrouillage des repas ou composants est prévu pour une version ultérieure.

## Régénération partielle

La V0 n'a pas besoin de proposer immédiatement :

* régénérer un seul repas ;
* régénérer un jour ;
* régénérer uniquement le sport ;
* conserver automatiquement certains éléments lors d'une régénération.

## Contexte de vie

Les éléments suivants ne sont pas nécessaires dans la première interface :

* enfants ;
* télétravail ;
* bureau ;
* vélo domicile-travail.

Ils seront ajoutés progressivement lorsque le moteur de génération commencera à les exploiter.

## Calendrier

La V0 n'est pas une vue type Outlook.

La grille hebdomadaire est dédiée à la planification alimentaire et sportive.

Une vue calendrier et une intégration Outlook restent des évolutions futures.

---

# Critère UX principal

Le Weekly Planner est réussi si :

1. la semaine entière peut être comprise rapidement ;
2. une modification de repas prend peu d'étapes ;
3. l'utilisateur n'est pas obligé d'ouvrir chaque repas pour comprendre son contenu ;
4. les informations nutritionnelles essentielles restent visibles sans surcharger la grille ;
5. l'application donne envie d'être consultée quotidiennement.

La priorité est la clarté, pas la densité fonctionnelle.
