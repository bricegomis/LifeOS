# Architecture et Guide iPhone LifeOS V1

Ce document détaille l'implémentation de la version mobile iPhone V1 de LifeOS.

---

## 1. Vue d'ensemble de l'architecture

LifeOS V1 pour iPhone combine le frontend Angular 22 avec le conteneur natif **Capacitor 7** et des extensions natives **SwiftUI / WidgetKit / ActivityKit / App Intents** (cible iOS 17.0+).

```
+--------------------------------------------------------------------------+
|                            iPhone LifeOS V1                              |
+--------------------------------------------------------------------------+
|  Frontend Angular 22 (Capacitor WebView)                                  |
|   - Synchronisation planning (2 semaines glissantes)                    |
|   - Rappels locaux (@capacitor/local-notifications)                     |
|   - Gestionnaire de session cuisine (découpage déterministe)             |
|   - Auth Supabase (PKCE, Custom URL Scheme lifeos://)                    |
+--------------------------------------------------------------------------+
       |
       | Capacitor Plugin: LifeosNativeBridge
       v
+--------------------------------------------------------------------------+
|  App Group partagé (group.fr.lifeos.app)                                 |
|   - widget_snapshot.json (atomique, zero JWT, identité foyer/user)       |
|   - cooking_session.json (session active, progression, concurrence)      |
+--------------------------------------------------------------------------+
       |
       +-------------------------------+
       |                               |
       v                               v
+------------------------------+ +-----------------------------------------+
|  WidgetKit Extension         | |  ActivityKit Live Activity              |
|   - Prochain repas / sport   | |   - Dynamic Island (compact / expanded) |
|   - Lock screen & Home screen| |   - Bannière écran verrouillé           |
|   - Progression de cuisine   | |   - App Intents (cocher sans Angular)   |
|   - ToggleCookingStepIntent  | |   - Expiration 8h max                   |
+------------------------------+ +-----------------------------------------+
```

### Principes directeurs
1. **Zéro fuite de jeton** : Le snapshot partagé dans l'App Group ne contient aucun token JWT ni identifiant sensible.
2. **Aucun accès réseau dans l'extension** : Le widget lit uniquement les données projetées par l'application principale.
3. **Une seule session de cuisine active** : Remplacement contrôlé avec boîte de dialogue de confirmation si une session existe déjà.
4. **App Intents autonomes (iOS 17+)** : `ToggleCookingStepIntent` permet de cocher ou décocher une étape de cuisine directement depuis le widget ou la Live Activity, sans avoir à lancer Angular.
5. **Rappels locaux opt-in** : Planifiés via `@capacitor/local-notifications`, calculés avec respect des fuseaux horaires (IANA) et du passage à l'heure d'été/d'hiver, plafonnés aux 50 prochaines notifications iOS. Événements sans horaire exclus des rappels.
6. **Isolation d'identité stricte** : Le snapshot est verrouillé sur l'identifiant utilisateur Supabase (`userId`) et le foyer réel (`householdId`). Toute divergence détectée ou déconnexion déclenche une purge immédiate.

---

## 2. Configuration et Prérequis de Compilation

### Environnement requis
- macOS avec Xcode 15+ ou 16+ (iOS SDK 17.0+)
- Node.js 20+ ou 24+
- CocoaPods (`pod 1.17+`)

### Identifiants et signature
- **Bundle ID Application** : `fr.lifeos.app`
- **Bundle ID Widget Extension** : `fr.lifeos.app.widget`
- **App Group** : `group.fr.lifeos.app` (activé sur les deux cibles via `App.entitlements`)
- **Custom URL Scheme** : `lifeos://` (enregistré dans `Info.plist` sous `CFBundleURLSchemes`). La V1 utilise le scheme personnalisé et non les Universal Links (les Universal Links nécessitent un fichier `apple-app-site-association` hébergé sur un domaine public HTTPS avec un Team ID Apple actif).

### Configuration Supabase requise
Dans le tableau de bord Supabase du projet :
1. Aller dans **Authentication > URL Configuration**.
2. Dans **Redirect URLs**, ajouter :
   ```
   lifeos://auth/callback
   ```
3. L'authentification utilise le flux PKCE avec ouverture via le navigateur système (`@capacitor/browser`). Lors de la redirection vers `lifeos://auth/callback?code=...`, l'application intercepte l'URL, ferme la fenêtre du navigateur, échange le code PKCE contre la session et navigue vers l'écran cible validé.

---

## 3. Commandes de Développement et de Compilation

Toutes les commandes s'exécutent depuis le dossier `src/frontend-angular` :

### 1. Installation des dépendances
```bash
cd src/frontend-angular
npm ci
```

### 2. Validation TypeScript, Lint et Tests unitaires
```bash
npm test
npm run type-check
npm run lint
```

### 3. Build Angular et synchronisation vers iOS
```bash
npm run build
npx cap copy ios
# ou directement
npm run cap:sync
```

### 4. Compilation native Xcode via ligne de commande
```bash
# Compilation de la Widget Extension
xcodebuild -workspace ios/App/App.xcworkspace \
  -scheme LifeosWidgetExtension \
  -destination 'generic/platform=iOS' \
  CODE_SIGN_IDENTITY="" CODE_SIGNING_REQUIRED=NO CODE_SIGNING_ALLOWED=NO clean build

# Compilation de l'Application principale
xcodebuild -workspace ios/App/App.xcworkspace \
  -scheme App \
  -destination 'generic/platform=iOS' \
  CODE_SIGN_IDENTITY="" CODE_SIGNING_REQUIRED=NO CODE_SIGNING_ALLOWED=NO clean build
```

---

## 4. Fonctionnalités et Flux Utilisateur

### 4.1 Mode Cuisine et Live Activity
1. **Lancement** :
   - Depuis la page **Recettes** (`/recipes`) : bouton "Cuisiner".
   - Depuis le **Planning** (`/planning`) : bouton "Préparer" sur les cartes de repas ayant une recette associée.
2. **Découpage déterministe** : Les instructions texte libre sont analysées et segmentées en étapes numérotées sans IA, avec prévisualisation et édition possible dans la boîte de dialogue avant démarrage.
3. **Session active** :
   - Un bandeau flottant s'affiche dans l'application avec les étapes et le compteur.
   - La Live Activity démarre avec l'île dynamique et l'écran verrouillé.
   - Le widget d'écran d'accueil bascule automatiquement sur l'état "En cuisine".
4. **App Intent natif** : Tap sur la case à cocher dans le widget ou la Live Activity invoque `ToggleCookingStepIntent`, mettant à jour `cooking_session.json` de façon atomique et rafraîchissant les timelines WidgetKit sans ouvrir l'application.

### 4.2 Prochain événement (Repas / Sport)
- Calcule les événements sur un horizon de 2 semaines glissantes sans création artificielle de semaine côté serveur.
- Convertit les dates civiles et minutes de début en timestamps ISO avec prise en compte du fuseau horaire de la semaine (`ManualWeek.timeZoneId`).
- **Gestion stricte des événements sans horaire** : Aucune heure inventée (startMinute et startIso sont `null`). Ils ne génèrent aucun rappel de notification locale. Sur les widgets (écran d'accueil et écran verrouillé), ils sont affichés avec la mention explicite `"Sans horaire"` ou `"—"` et classés après les événements programmés de la journée.
- Les widgets ouvrent l'application via des deep links directs : `lifeos:///planning` (avec paramètre de date `?date=YYYY-MM-DD` si applicable), validés pour n'autoriser que les routes internes légitimes.

### 4.3 Rappels locaux
- Configurables dans la page **Paramètres** (`/settings`).
- Opt-in désactivé par défaut ; demande d'autorisation système uniquement lors de l'activation.
- Choix d'avance : 0 min, 5 min, 15 min, 30 min.
- Identifiants de notifications stables générés par hachage 32-bit (`hashNotificationId`).
- Reprogrammation idempotente à chaque modification du planning ou changement d'avance.
- Les événements passés ou sans horaire sont systématiquement ignorés pour les rappels.

### 4.4 Synchronisation et Résilience
- Synchronisation automatique au premier affichage, lors du retour au premier plan (`appStateChange: isActive`), et après chaque mutation du planning (création, modification, suppression d'un repas ou d'une séance).
- **Résilience hors-ligne** : Si l'API est indisponible ou le réseau inaccessible, le cache local n'est **jamais** écrasé par un snapshot vide. Le statut passe en état `stale` (ou `error`), visible dans les réglages avec la date de la dernière synchronisation réussie et le libellé de l'incident.
- **Isolation d'identité** : Le snapshot contient l'identifiant réel de l'utilisateur (`userId`) et du foyer (`householdId` résolu via `/api/households/current`). Au démarrage et à chaque synchronisation, toute divergence d'identité entraîne une purge complète des données du foyer précédent.
- Lors de la déconnexion (`AuthService.signOut`), `NativeSyncService.purgeAll()` est exécuté :
  - Annulation de toutes les notifications locales en attente.
  - Suppression de `widget_snapshot.json` et `cooking_session.json`.
  - Fin immédiate des Live Activities en cours.
  - Rafraîchissement des widgets (retour à l'état "Connexion requise").

---

## 5. Limites V1 et Perspectives V2

1. **Pas de synchronisation multi-appareils temps réel** : La V1 est locale à cet iPhone. Toute modification effectuée sur un autre appareil nécessite la réouverture de l'application sur l'iPhone pour actualiser le cache et les rappels locaux.
2. **Pas de push notifications distantes APNs** : Les rappels sont exclusivement locaux (`UNUserNotificationCenter`).
3. **Session de cuisine unique** : Une seule recette peut être cuisinée à la fois (session active maximale de 8h).
