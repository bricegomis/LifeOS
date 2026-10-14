# LifeOS iPhone — installation personnelle gratuite et widgets

LifeOS utilise Angular 22 dans une WebView Capacitor 7, avec une vraie extension
SwiftUI/WidgetKit, des App Intents et une Live Activity ActivityKit. Cible :
**iOS 17 minimum**. Les widgets sont conservés ; ni raccourci, ni endpoint public
ne remplace le partage local. Supabase gère uniquement l'authentification ; les
données de foyer restent dans LifeOS API/PostgreSQL.

## Gratuit, mais pas permanent sans maintenance

L'installation visée est **Xcode → iPhone personnel**, avec un compte Apple gratuit
et sa **Personal Team**, sans App Store, TestFlight ni abonnement Apple Developer.
La [table Apple des capacités](https://developer.apple.com/help/account/reference/supported-capabilities-ios)
actuelle indique **oui pour App groups et Keychain sharing dans les trois
colonnes**, y compris « Apple Developer » gratuit (HTML `icon-checksolid`,
`alt="yes"`, vérifié le 13 octobre 2026). Certaines conversions Markdown omettent
ces icônes ; ne pas conclure à une capacité payante à partir de cette conversion.
LifeOS ne demande pas Keychain sharing et ne partage aucun JWT avec l'extension.

La table ne remplace pas une preuve de provisioning : **la signature réelle des
deux cibles avec la Personal Team et le fonctionnement sur iPhone restent à
vérifier**. Aucun compte, certificat ni profil personnel n'a été consulté ou
exporté pendant ce lot.

Apple documente dans
[About your developer account](https://developer.apple.com/help/account/basics/about-your-developer-account)
un maximum de 10 App IDs, 3 appareils et 3 apps par appareil pour ce mode.
Les profils expirent **7 jours après émission** : reconstruire et réinstaller
depuis Xcode, avec l'iPhone disponible, chaque semaine. Les App IDs/appareils
enregistrés sont également soumis à l'expiration indiquée par Apple. L'app et
son extension ont des identifiants distincts ; vérifier les quotas affichés par
Xcode. Ne pas promettre une installation gratuite permanente.

## Prérequis et configuration publique

Travailler dans un checkout isolé contenant la dernière `main`, jamais dans
un ancien checkout partagé. Outils validés localement : Node **24.15.0**,
npm **11.12.1**, CocoaPods **1.17.0**, Xcode **27.0 (27A266a)**.
Le manifeste exige Node `^22.22.3 || ^24.15.0 || >=26.0.0` ; Node 20 est insuffisant.
Xcode doit disposer d'un SDK iOS compatible et de composants système à jour ;
les compilations CI utilisent le Xcode du runner macOS, sans compte Apple.

Depuis `src/frontend-angular`, copier `.env.example` vers `.env.local` (non suivi)
et renseigner :

```dotenv
VITE_LIFEOS_API_URL=https://votre-api.example
VITE_SUPABASE_URL=https://votre-projet.supabase.co
VITE_SUPABASE_ANON_KEY=cle-publique-anon-ou-publishable
```

L'URL API **ne contient pas `/api`** : le client l'ajoute. Ne mettre ni clé
`service_role`, ni `sb_secret_`, ni mot de passe, ni chaîne PostgreSQL dans le
frontend. Ces trois valeurs sont publiques et embarquées dans `config.js`, pas
chargées depuis les variables d'un serveur Docker une fois l'app installée.
Changer `.env.local` impose build, synchronisation et réinstallation.

`npm run cap:sync` vérifie le **`config.js` du bundle produit** : HTTPS, URL API
sans double `/api`, clé Supabase anon/publishable uniquement. Cela ne prouve pas
la disponibilité réelle des services. Ne pas désactiver ATS pour contourner HTTP.

Actions externes à réaliser par l'utilisateur/administrateur, sans les modifier
dans ce lot :

- API : autoriser exactement l'origine **`capacitor://localhost`** dans
  `Cors:AllowedOrigins` pour la configuration Capacitor par défaut ; garder les
  origines web nécessaires. Ne pas remplacer par `AllowAnyOrigin`, ni retirer
  JWT/isolation foyer. Certificat HTTPS valide et API accessible depuis l'iPhone.
- Supabase Auth → URL Configuration : autoriser
  `lifeos://auth/callback` **avec les paramètres de retour utilisés**. Vérifier
  également `lifeos://auth/callback?redirect=%2Fplanning` ; au besoin utiliser
  `lifeos://auth/callback**` dans la liste de redirections du projet de confiance.
  Activer/configurer Google et la connexion e-mail selon [le guide OAuth](google-oauth.md).
- Un lien PKCE doit revenir sur **cet iPhone qui l'a demandé**, où réside le
  vérificateur. Les erreurs d'expiration/refus/vérificateur sont affichées sur
  Login ; ne pas copier le code vers un autre appareil. Les callbacks froids et
  chauds passent par la même validation, avec échange dédupliqué en mémoire.

## Identifiants personnalisables, sans Team personnelle dans Git

Base : `src/frontend-angular/ios/App/Config/LifeOS.xcconfig`.
Les configurations App Debug/Release incluent aussi les réglages CocoaPods.
Créer facultativement **`Config/Local.xcconfig`** (ignoré par Git), par exemple :

```xcconfig
LIFEOS_APP_BUNDLE_ID = fr.votreidentifiant.lifeos
LIFEOS_WIDGET_BUNDLE_ID = fr.votreidentifiant.lifeos.widget
LIFEOS_APP_GROUP = group.fr.votreidentifiant.lifeos
```

Les valeurs de démonstration du dépôt sont `fr.lifeos.app`,
`fr.lifeos.app.widget`, `group.fr.lifeos.app`. Utiliser des identifiants personnels
uniques si Xcode signale qu'ils sont déjà utilisés. Les deux cibles doivent avoir
**le même App Group** : leurs entitlements et Info.plist utilisent
`$(LIFEOS_APP_GROUP)`, et Swift lit `LifeosAppGroupIdentifier` dans son bundle.
Foundation est référencé via `SDKROOT`, sans version de SDK figée.

**Actions humaines Apple nécessaires** :

1. Dans Xcode → Settings → Accounts, se connecter soi-même au compte Apple gratuit.
   Aucun certificat/credential ne doit être transmis à l'agent.
2. Ouvrir **`ios/App/App.xcworkspace`**, pas seulement `.xcodeproj`. Dans Signing &
   Capabilities, sélectionner **la même Personal Team** et la signature automatique
   pour **App et LifeosWidgetExtension** ; conserver App Groups sur les deux.
   Si le choix écrit un `DEVELOPMENT_TEAM` personnel dans le projet, ne pas le
   committer : un override `DEVELOPMENT_TEAM = ...` peut rester uniquement dans
   `Config/Local.xcconfig`.
3. Brancher/déverrouiller l'iPhone, accepter « Faire confiance », l'associer à
   Xcode et activer **Réglages → Confidentialité et sécurité → Mode développeur**,
   avec redémarrage/confirmation si demandés.
4. Choisir le scheme partagé **App** et l'iPhone réel comme destination, puis
   **Run**. Accepter si nécessaire le développeur dans Réglages → Général →
   VPN et gestion de l'appareil. Vérifier que l'extension est bien embarquée.

**Si le provisioning refuse effectivement l'App Group**, conserver le message
exact de Xcode, les noms des capacités et les cibles concernées (sans certificat,
token ni profil exporté). Vérifier correspondance Team/IDs/Group et composants
Xcode. Ne pas annoncer les widgets opérationnels avant signature et test réels.
Ne pas acheter d'abonnement, supprimer les widgets, publier des données ou
refondre le serveur comme « contournement » sans nouvelle décision.

## Commandes reproductibles

Depuis `src/frontend-angular` :

```bash
npm ci
npm test
npm run lint
npm run type-check
npm run cap:sync
open ios/App/App.xcworkspace
```

Depuis la racine du dépôt, le test Swift sans compte Apple :

```bash
bash scripts/test-ios-contract.sh
```

Il compile les DTO et l'IO coordonnée avec `swiftc`, échange réellement du JSON
TypeScript/Swift et lance des mutations concurrentes dans des processus distincts.
Le test ne prouve pas les entitlements sur appareil.

Compilation **sans signature** depuis `src/frontend-angular` :

```bash
xcodebuild -workspace ios/App/App.xcworkspace -scheme App \
  -configuration Debug -destination 'generic/platform=iOS Simulator' \
  -derivedDataPath ios/DerivedData/validation-simulator \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build

xcodebuild -workspace ios/App/App.xcworkspace -scheme App \
  -configuration Release -destination 'generic/platform=iOS' \
  -derivedDataPath ios/DerivedData/validation-device \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
```

Le scheme App construit et embarque **LifeosWidgetExtension**. Un scheme partagé
de l'extension permet aussi sa compilation séparée. La CI
`.github/workflows/ios-validation.yml` exécute contrôles Angular, contrat Swift,
sync Capacitor et builds natifs sans secret Apple ; sa configuration réseau
`.example` est synthétique et **ne permet pas une vraie connexion**.

## Fonctionnement et corrections de fiabilité

Le plugin local Swift `CAPBridgedPlugin` est enregistré explicitement via
`LifeosBridgeViewController.capacitorDidLoad()` (storyboard App).
L'ancienne macro Objective-C `CAP_PLUGIN` seule ne découvrait pas ce plugin local
avec Capacitor 7. Réglages distingue navigateur web, pont disponible et plugin
non enregistré ; les promesses des listeners sont attendues et les incidents
d'ouverture de lien remontent sans rejet silencieux.
Les registrations URL et notifications sont indépendantes et retentables :
un refus du listener notifications ne bloque pas le callback de lancement à froid.

Le snapshot `widget_snapshot.json` contient utilisateur/foyer, date de génération,
fuseau et événements repas/sport des deux semaines glissantes. Il ne contient
**aucun JWT** et l'extension ne fait aucun accès réseau. Les dates civiles utilisent
le fuseau IANA de la semaine, y compris DST ; les événements sans heure restent
sans heure et ne génèrent pas de rappel. Le widget ne réaffiche pas un événement
passé par simple fallback. Une mutation du planning et le retour au premier plan
demandent une synchronisation. Une panne réseau garde le dernier snapshot de
**la même identité**, avec incident dans Réglages.
Les demandes concurrentes attendent aussi leur resynchronisation regroupée avant
de déclarer les rappels prêts. Une fois l'identité du foyer vérifiée, la cuisine
est restaurée avant les requêtes planning : leur échec ne bloque pas cette lecture
locale. Si le foyer ne peut pas être vérifié, cette restauration reste bloquée.

La cuisine a **un fichier autoritatif unique** `cooking_session.json`, sans copie
dupliquée dans le snapshot planning :

- Contrat v2 : `startedAt`, `updatedAt`, `revision`, indices bornés/uniques ;
  migration en lecture des anciens payloads TS et Swift (`startedAtIso`).
  L'étape courante et l'état terminé sont dérivés, pas exigés au décodage.
- App et App Intent envoient des **mutations ciblées** de session sous
  `NSFileCoordinator`, pas un état complet Angular périmé. Chaque mutation
  incrémente la révision ; une action visant une ancienne session est refusée.
- Une session expire **8 h après `startedAt`**, sans prolongation à chaque case.
  Restauration, widget et mutation refusent l'état expiré/futur. La Live Activity
  utilise cette même échéance et masque ses actions lorsqu'elle devient stale ;
  restauration/déconnexion la termine. `staleDate` n'est pas un minuteur qui
  exécute du code et termine automatiquement l'activité au moment exact.
- La purge efface mémoire et ancien cache JS cuisine, fichiers partagés,
  notifications et Live Activities, et réinitialise l'opt-in rappels.
  Une barrière invalide les opérations en vol ; toute écriture déjà admise finit
  **avant** la purge. Elle ne peut pas republier après celle-ci. Aucune
  restauration native ne retombe sur une vieille copie `localStorage`.
- Les erreurs de lecture/mutation sont explicites. Live Activities peuvent être
  désactivées par iOS : la checklist reste utilisable, avec statut distinct.

Les rappels restent **locaux et opt-in**, plafonnés aux 50 prochains rappels.
Le refus d'autorisation est signalé ; désactivation/refus annulent les rappels
restants. Le tap ouvre la route planning validée. Les permissions ne sont jamais
demandées silencieusement au lancement.

## Preuves du lot et vérification à faire sur iPhone

Au 13 octobre 2026 : dernière `origin/main` vérifiée (`dd5beea`), tests Node,
lint, type-check, build et `cap:sync` exécutés ; roundtrips Swift/TS, bornes,
expiration, purge/isolation, callbacks et concurrence multiprocessus exécutés.
App **et extension** compilées sans signature pour simulateur et appareil
générique avec Xcode 27. Le bundle de validation utilise exclusivement des valeurs
publiques synthétiques ; **connexion aux services réels non testée**.

Sur ce Mac, Xcode avertit d'un symbole CoreDevice manquant
`DetailedOperation.metrics` et de CoreSimulator **1051.55.0 < 1171.7.0**.
Les compilations génériques ont réussi malgré ces avertissements ; lancement
du simulateur et gestion d'un iPhone ne sont pas prouvés. Faire mettre à jour /
réparer Xcode et ses composants système par l'utilisateur avant le test appareil ;
ne pas contourner la panne par des suppressions de frameworks.

Après signature personnelle et configuration réelle, vérifier manuellement :

| Parcours | Résultat attendu |
| --- | --- |
| Connexion Google + lien e-mail, app fermée puis ouverte | Session sur cet iPhone, destination conservée ; refus/lien expiré expliqués, pas de double échange PKCE. |
| Modifier repas/sport, revenir au premier plan | Snapshot et widget réels actualisés ; jour/heure conformes au fuseau de la semaine. |
| Ajouter widget accueil et écran verrouillé | Appui long → ajouter LifeOS ; aucun placeholder permanent ni donnée d'un autre compte. |
| Cuisine depuis recette/planning, app et widget alternativement | Cases conservées des deux côtés, pas de mise à jour perdue ; Live Activity si autorisée. |
| Terminer, remplacer, attendre expiration | Anciennes actions refusées ; aucun état expiré restauré, activité terminée à la reprise. |
| Refuser puis autoriser rappels ; tap d'un rappel | Incident compréhensible, pas d'opt-in automatique, navigation au bon jour. |
| Mode avion, fermeture forcée, redémarrage | Snapshot widget conservé pour la même identité ; aucune prétention de planning Angular hors ligne. |
| Déconnexion/changement de compte pendant sync | Widgets sans anciennes données, cuisine/rappels effacés ; pas de réécriture tardive. |
| Après 7 jours | Reconstruction/réinstallation Xcode réussie ; recontrôler widget et permissions. |

**Non validés tant que ces actions n'ont pas été faites** : provisioning Personal
Team, installation et exécution réelles iPhone, OAuth/config CORS réelle,
rafraîchissement WidgetKit/ActivityKit par iOS, livraison/tap des notifications.
Les cadences WidgetKit sont décidées par iOS ; `reloadAllTimelines` est une demande,
pas une garantie d'actualisation immédiate.

Le cache du widget n'est **pas** un cache de consultation Angular : le planning
dans l'app dépend encore de l'API. Hors ligne, pas de planning de démonstration,
pas d'écriture silencieuse. Les améliorations fonctionnelles sont inscrites dans
[la roadmap](03-roadmap.md), pas implémentées ici. Pas de push APNs, HealthKit,
scan caméra, stock ou courses ; aucune publication ni intervention production.
