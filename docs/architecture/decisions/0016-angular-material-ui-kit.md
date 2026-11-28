# ADR 0016 — Kit d'interface : Angular Material

Statut : accepté, 2026-11-16 ; maquette Sport en Material 3 validée le même jour. Lève la
règle « contrôles HTML natifs, aucune bibliothèque d'interface ». Appliqué à toute
l'application le 2026-11-22 : Sport ([ADR 0015](0015-sport-pages-and-routine-sessions.md)),
Alimentation, avis du foyer, Paramètres et connexion. C'est la norme de tout nouvel écran.
Complété le 2026-11-28 (revue UX, plan 1) : menu, barre du haut du téléphone, coche immédiate,
chargement et ton.

## Contexte

L'interface est faite de contrôles HTML natifs et de quelques blocs CSS partagés
(boutons, champs, cartes, barre de filtres) dans `src/styles.css`, plus une fenêtre
commune (`lifeos-dialog`). Chaque écran est donc dessiné à la main, en maquette comme
en code : la place de la photo, des actions, du bouton d'ajout ou de l'action
dangereuse se décide écran par écran, et la cohérence dépend de la mémoire de chacun.
Lors de la maquette de l'ADR 0015, l'utilisateur a dû redemander la même disposition
pour chaque carte (2026-11-16).

L'utilisateur veut une interface cohérente de bout en bout, moderne, utilisable
d'abord au téléphone (app iPhone par Capacitor) et sur ordinateur pour gérer les
bibliothèques. La règle « aucune bibliothèque d'interface » n'a plus lieu d'être
(décision de l'utilisateur, 2026-11-16).

Comparés le 2026-11-16, tous compatibles avec Angular 22 : Angular Material, PrimeNG,
Taiga UI, Ionic, Spartan UI, NG-ZORRO.

## Décision

**Angular Material**, en Material Design 3, avec le CDK Angular.

- Officiel : publié par l'équipe Angular, à chaque version d'Angular ; c'est le
  choix le plus sûr pour une application qui doit durer.
- Accessible (clavier, lecteurs d'écran) et adapté à l'ordinateur comme au
  téléphone.
- Ses conventions fixent la place des éléments : on n'a plus à décider, écran par
  écran, où va un bouton.

Les autres kits sont écartés : PrimeNG et Taiga UI sont plus fournis mais moins sûrs
dans la durée ; Ionic est pensé d'abord pour le mobile et imposerait de refaire la
navigation ; Spartan UI demande Tailwind et laisse la cohérence à notre charge ;
NG-ZORRO vise les outils de gestion d'entreprise.

### Thème LifeOS

Un seul thème Material 3, déclaré une fois :

- couleur principale tirée du vert LifeOS (`#356d4f`), fond et surfaces beiges
  (`#f7f3ea`) ;
- police Inter, sur l'échelle typographique de Material 3 ;
- arrondis de Material 3 (cartes, fenêtres, boutons) ;
- **mode sombre** suivant le réglage du système, fourni par le même thème.

Les couleurs viennent uniquement des variables du thème (`--mat-sys-*`) : les anciennes
variables `--lifeos-*` et `src/styles.css` ont disparu avec la migration. Repères : un
fond tertiaire (`tertiary-container`) signale ce qui reste à compléter ou un total
partiel ; un fond d'erreur doux (`error-container`), un aliment hors saison.

### Icônes

**Material Symbols** (arrondis), embarquées en SVG une par une
(`@material-symbols/svg-400`, registre `shared/ui/icons.ts`) : seules les icônes
utilisées sont livrées, rien n'est chargé depuis Google, et l'app iPhone fonctionne
sans réseau. La police complète (plus de 5 Mo) a été écartée pour cette raison.
PrimeIcons est retiré.

### Conventions d'écran

Elles valent pour tous les modules ; les composants LifeOS partagés les appliquent.

| Élément | Convention |
| --- | --- |
| Navigation | Menu latéral (`mat-sidenav`) avec un groupe par domaine qui s'ouvre (Alimentation, Sport…), Paramètres en bas. Seul le groupe de la page courante est ouvert à l'arrivée sur une page ; un groupe ouvert à la main le reste jusqu'au changement de page. Sur ordinateur il reste affiché ; sur téléphone il s'ouvre depuis la barre du haut (ADR 0015). |
| Barre du haut | Fixe, sur ordinateur et téléphone : bouton menu (replie le menu sur ordinateur, l'ouvre sur téléphone), « LifeOS » sur ordinateur. Sur téléphone, 56 px et le titre de la page seul (« Routines », « Sport » pour l'accueil du module) ; sur une page détail (un todo, un livre…), une flèche de retour remplace le bouton menu et le lien de retour de la page n'est affiché que sur ordinateur (`lo-back`, `lo-back-link`). Seul le contenu sous la barre défile. |
| Page de liste | Champ de filtre en haut, puis une liste ou des cartes. **Ajouter** : bouton flottant « + » en bas à droite sur téléphone, bouton principal dans la barre de la page sur ordinateur. |
| Élément d'une liste | Photo ou vignette à gauche, titre et résumé au centre, menu « ⋯ » ou action principale à droite. Un appui sur l'élément l'ouvre. Sans photo, une icône en tient lieu. |
| Création et modification | Fenêtre de dialogue sur ordinateur ; **plein écran sur téléphone**. « Enregistrer » en haut à droite (téléphone) ou en bas à droite (ordinateur) ; l'action dangereuse (archiver, arrêter, supprimer) en bas à gauche, avec confirmation. Fermer un brouillon modifié demande d'abord. |
| Retour d'une action | Bandeau bref en bas (`mat-snack-bar`) : « Routine enregistrée ». Une coche s'affiche tout de suite, avant la réponse du serveur ; seul l'élément touché attend, et une erreur annule la coche. |
| Chargement | Emplacements gris à la taille du contenu attendu (`app-skeleton`, en liste ou en page), jamais un « Chargement… » seul. |
| Ton | Tutoiement dans toute l'interface (« Ajoute », « Ta cible »), décidé le 2026-11-28. |
| Choix | Pastilles au dessin des chips Material 3 (`lo-chip`, `lo-filter-chip` pour un filtre) pour plusieurs choix, bouton segmenté (`mat-button-toggle`) pour deux à quatre options exclusives, liste déroulante (`mat-select`) au-delà. |
| Saisie de nombres courts | Compteur − / + (composant LifeOS, sur les boutons Material). |

Composants LifeOS à construire sur Material quand le kit ne les a pas : compteur
− / +, bouton de progression (« Série faite » qui se remplit), champ photo avec ses
icônes modifier et supprimer, vignette d'image chargée par l'API.

## Mise en œuvre

- `src/theme.scss` déclare le thème ; `src/lifeos-ui.css` porte la base des pages et les
  classes `lo-*` partagées : page et en-tête, liste et ligne (`lo-list`, `lo-row`), carte et
  panneau (`lo-card`, `lo-panel`), tableau en grille (`lo-table`), statistiques
  (`lo-stat`), étiquettes (`lo-tag` et ses variantes), pastilles de filtre et de choix
  (`lo-filter-chip`, `lo-chip`), messages (`lo-notice`, `lo-error`, `lo-status`),
  affichage ordinateur ou téléphone seul (`lo-desktop-only`, `lo-phone-only`).
- `shared/ui` fournit les composants LifeOS : page de bibliothèque (`app-list-page`, avec
  recherche, filtre des archivés, bouton d'ajout et actions secondaires), cadre de fenêtre
  (`app-editor-frame`, ouvert par `openEditor`, plein écran sur téléphone ; sans
  `formId`, une fenêtre dont chaque ligne s'enregistre seule n'a que « Fermer »),
  compteur − / +, emplacements de chargement (`app-skeleton`), registre d'icônes, petite fenêtre de confirmation (`ConfirmService`,
  `shared/ui/confirm.ts`) pour toute question avant une action hors d'une fenêtre de
  modification ; les boîtes natives du navigateur (`confirm`, `alert`) ne sont pas utilisées.
- Toutes les fenêtres passent par `MatDialog` ; la garde de brouillon modifié
  (`dialog-guard.ts`) est conservée. L'ancienne fenêtre `lifeos-dialog` est supprimée.
- Champs : `mat-form-field` en contour avec `matInput` ou `mat-select`, cases
  `mat-checkbox`. Un choix de fichier reste un champ natif caché, ouvert par un bouton
  Material.
- Le test `src/app/ui-kit.spec.ts` échoue si un écran réintroduit un `select`, un
  `input` ou un `textarea` natif, PrimeIcons, une variable `--lifeos-*`, une couleur
  écrite en dur ou une ancienne classe partagée.
- Les maquettes suivent ces conventions et l'apparence de Material 3.

## Conséquences

- Nouvelles dépendances : `@angular/material`, `@angular/cdk`, `@material-symbols/svg-400`.
- Mode sombre actif depuis le 2026-11-22, selon le réglage du système : chaque variable
  du thème vaut `light-dark(clair, sombre)` ; les surfaces beiges deviennent les tons
  neutres chauds et foncés de la même palette. Les vignettes sans photo gardent leur
  teinte, assombrie en mode sombre. Un écran se vérifie dans les deux modes.
- Taille de l'application en hausse, limitée par le chargement à la demande des modules.
- Les règles « contrôles natifs, aucune bibliothèque d'interface » sont retirées de
  `CLAUDE.md`, `AGENTS.md` et `src/frontend-angular/CLAUDE.md`, qui imposent désormais
  Material pour tout écran.
- Les tests de composants passent par les `ComponentHarness` de Material plutôt que
  par des sélecteurs CSS.
- Code Swift non touché (étape 6).
