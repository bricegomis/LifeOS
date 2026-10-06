# Connexion Google avec Supabase Auth

Le frontend **Angular** publié (`src/frontend-angular`) propose « Continuer avec
Google » en premier, puis le lien magique e-mail en secours. Le frontend Vue
historique n'est pas modifié. Aucun déploiement ni activation de fournisseur
n'est effectué par ce changement : les opérations de console ci-dessous restent
à réaliser par un administrateur.

## Parcours et limites

Le SDK officiel `@supabase/supabase-js` lance `signInWithOAuth` dans le **même
onglet**, avec le client existant en PKCE. Google renvoie vers Supabase, qui
renvoie un code vers LifeOS. LifeOS capture et retire ce code de l'URL puis
l'échange **une seule fois** avec `exchangeCodeForSession`. Les refus, erreurs
d'échange et codes sans session ne sont pas traités comme une connexion réussie.
Le retour demandé est limité aux routes internes connues ; une URL externe,
une route inconnue ou `/login` est remplacée par `/`.

La clé de stockage reste `lifeos.supabase.auth`, avec persistance et renouvellement
automatique des jetons. Le service observe toujours les changements de session
et la déconnexion Supabase. L'API ASP.NET Core reçoit uniquement le **JWT
Supabase**, jamais un jeton Google ; sa validation issuer/audience/signature et
la résolution du foyer à partir du `sub` ne changent pas.

En navigation privée, lancer Google puis terminer le parcours **sans fermer la
fenêtre privée ni changer de navigateur** conserve le vérificateur PKCE. Cela
évite le problème du lien e-mail ouvert dans un autre navigateur. Google ne
rend toutefois pas PKCE indépendant du navigateur : supprimer le stockage,
fermer toutes les fenêtres privées ou transférer le callback casse l'échange.
Fermer la fenêtre sans retour OAuth n'envoie aucune erreur à LifeOS ; si Google
renvoie un refus, LifeOS l'affiche. Un retour arrière restaure le bouton.

Le lien e-mail PKCE existant conserve sa contrainte de navigateur. Les liens
`token_hash` restent pris en charge, si le modèle d'e-mail Supabase a déjà été
adapté ; aucun changement de modèle n'est requis pour Google.

## Retrouver un compte existant

Utiliser **la même adresse Google vérifiée** que l'adresse confirmée utilisée
pour le lien magique, dans **le même projet Supabase**. Supabase effectue
lui-même la liaison automatique des identités ayant la même adresse et, lorsque
la liaison réussit, conserve l'utilisateur Supabase et donc son `sub` / foyer.
Vérifier ce résultat sur un compte existant avant de généraliser.

Ce n'est pas une fusion de foyers ni une garantie de récupération pour une
adresse différente (alias, autre compte Google, autre projet Supabase). Une
nouvelle identité non liée peut créer un autre utilisateur et donc un autre
foyer. Les utilisateurs SAML SSO sont exclus de la liaison. Supabase protège
également la liaison contre les adresses non vérifiées et peut retirer les
identités non confirmées. LifeOS ne fait **aucune fusion maison par e-mail** et
ne propose pas de liaison manuelle d'adresses différentes dans cette étape.

## Configuration manuelle Google Cloud

1. Créer ou sélectionner un projet dans [Google Cloud](https://console.cloud.google.com/).
   Ouvrir **Google Auth Platform** (ancien écran de consentement OAuth).
2. Dans **Branding**, renseigner le nom LifeOS, l'e-mail d'assistance et les
   coordonnées du développeur. Renseigner les domaines, liens et éventuelle
   vérification de marque demandés par Google pour votre publication.
3. Dans **Audience**, choisir l'audience adaptée. **Internal** limite l'accès à
   l'organisation Google Workspace concernée. Pour des comptes personnels,
   choisir **External** ; en statut **Testing**, ajouter explicitement les
   comptes de validation dans **Test users**. Passer à la publication appropriée
   avant d'ouvrir l'accès général, et suivre les vérifications exigées par Google.
4. Dans **Data Access**, conserver uniquement les scopes de connexion :
   `openid`, `https://www.googleapis.com/auth/userinfo.email` et
   `https://www.googleapis.com/auth/userinfo.profile`. Aucune permission Gmail,
   Calendar ou Drive n'est nécessaire.
5. Dans **Clients**, créer un client OAuth de type **Web application**.
   Renseigner les **Authorized JavaScript origins**, sans chemin ni fragment :
   `http://localhost:4200` pour Angular local et l'origine HTTPS de production
   (par exemple `https://lifeos.example`). Un déploiement sous `/LifeOS/` garde
   l'origine `https://lifeos.example`. Retirer les origines locales devenues inutiles.
6. Dans Supabase → **Authentication → Sign In / Providers → Google**, copier
   exactement l'URL **Callback URL** affichée. La coller dans les
   **Authorized redirect URIs** du client Google. Elle ressemble à
   `https://<project-ref>.supabase.co/auth/v1/callback`, ou utilise le domaine
   personnalisé Supabase. **Ce n'est pas l'URL frontend `#/login`.**
   Avec Supabase CLI local seulement, le callback est généralement
   `http://127.0.0.1:54321/auth/v1/callback` : utiliser la valeur de votre instance.
7. Enregistrer le client. Copier son **Client ID** et son **Client Secret**
   directement dans le fournisseur Google de Supabase, activer le fournisseur
   puis sauvegarder. Ne pas coller le secret dans un chat, un dépôt, une variable
   frontend ou `config.js`. Garder les protections de nonce par défaut.

## Configuration manuelle Supabase et LifeOS

Dans Supabase → **Authentication → URL Configuration** :

- **Site URL** : l'URL publique de base de LifeOS, par exemple
  `https://lifeos.example/` (ou `https://lifeos.example/LifeOS/`).
- **Redirect URLs** : autoriser le retour frontend généré par LifeOS.
  En local, `http://localhost:4200/**` couvre le serveur Angular.
  Ajouter `http://127.0.0.1:4200/**` uniquement si cette origine est utilisée.
  En production, limiter au véritable hôte et au callback login, par exemple
  `https://lifeos.example/#/login**`, ou
  `https://lifeos.example/LifeOS/#/login**` pour un sous-chemin.
  Le suffixe couvre le paramètre `redirect` encodé des routes internes, pas
  tous les hôtes ni tous les chemins de l'application. Pour une liste totalement
  exacte, inscrire chaque URL de callback utilisée ; exemples :
  `https://lifeos.example/#/login?redirect=%2F` et
  `https://lifeos.example/#/login?redirect=%2Fplanning`.
  Conserver les entrées nécessaires aux liens magiques et à leurs modèles
  existants. Une entrée manquante peut faire retomber sur le Site URL et perdre
  la destination demandée.

Il y a donc **deux retours distincts** :

| Emplacement de configuration | Destination |
| --- | --- |
| Google → Authorized redirect URIs | Callback **Supabase** `/auth/v1/callback` |
| Supabase → Redirect URLs | Callback **LifeOS** `/#/login?redirect=...` |

LifeOS n'a besoin d'**aucune nouvelle variable Google**. Conserver les variables
runtime existantes :

```text
VITE_SUPABASE_URL=https://<project-ref>.supabase.co
VITE_SUPABASE_ANON_KEY=<cle-publique-anon>
VITE_LIFEOS_API_URL=https://<api-host>
```

En local, elles sont lues dans `src/frontend-angular/.env.local` par
`npm run dev`. En Docker/Coolify, elles sont fournies au conteneur frontend et
écrites dans `config.js` au démarrage. L'API doit conserver le même projet
`SUPABASE_URL` et autoriser l'origine frontend via CORS. Ne jamais utiliser une
clé `service_role` dans le frontend. Sans configuration Supabase, les deux
actions sont désactivées et un message explicite apparaît. La présence de
variables frontend **ne prouve pas** que le provider Google est activé.

## Validation finale, notamment Firefox privé

Les tests automatisés (`npm test` dans `src/frontend-angular`) couvrent le
lancement SDK Google, les callbacks PKCE et token-hash, les erreurs/refus, le
nettoyage d'URL, la restauration, les retours internes et un échange PKCE avec
le véritable SDK et un transport simulé, ainsi que le renouvellement d'une
session expirée et la déconnexion. Le lint et le build Angular contrôlent
également TypeScript et les templates. Ils ne remplacent pas un E2E Google.

Après les configurations de console et une publication autorisée :

1. Sur un compte e-mail existant, relever de façon privée son utilisateur
   Supabase / foyer et vérifier ses données. Ne pas exporter les jetons.
2. Ouvrir **une nouvelle fenêtre privée Firefox**, aller directement à
   `https://<host>/#/planning` (adapter le sous-chemin), constater le login, puis
   activer « Continuer avec Google » au clavier. Choisir l'adresse vérifiée
   identique au compte e-mail. Garder la même fenêtre ouverte.
3. Terminer Google et vérifier le retour à `/planning`, l'absence de `code` /
   paramètres sensibles dans l'URL, le même utilisateur Supabase et les mêmes
   données de foyer. Vérifier que l'API accepte le JWT Supabase.
4. Recharger la page : la session doit être restaurée. La laisser vivre au-delà
   de l'expiration du jeton d'accès, puis appeler une page API pour vérifier son
   renouvellement. Fermer toutes les fenêtres privées efface normalement cette
   session ; une nouvelle fenêtre privée doit demander une connexion.
5. Se déconnecter ; les routes protégées doivent revenir au login. Relancer
   Google puis refuser/annuler avec un retour à LifeOS : vérifier le message et
   la possibilité de réessayer. Tester aussi le retour arrière.
6. Tester le secours e-mail dans la même fenêtre. Vérifier les liens expirés et,
   si configuré, le modèle `token_hash`. Tester une destination
   `redirect=https://example.org` : le retour doit être `/`, jamais ce site.
7. Avec un autre compte Google de test, vérifier que les données du premier
   foyer ne sont pas visibles (aucune fusion ni modification de l'isolation).

**Non vérifié sans accès aux consoles et comptes de test :** activation réelle
du fournisseur, audience Google, correspondance exacte des URLs de déploiement,
liaison réelle du compte existant, connexion Google réelle et comportement
Firefox privé de bout en bout. Ces vérifications restent manuelles.

## Sources officielles

Consultées le 6 octobre 2026 :

- [Google avec Supabase](https://supabase.com/docs/guides/auth/social-login/auth-google)
- [URLs de redirection Supabase](https://supabase.com/docs/guides/auth/redirect-urls)
- [Liaison d'identités Supabase et limites](https://supabase.com/docs/guides/auth/auth-identity-linking)
