# Connexion Google

LifeOS API connecte les utilisateurs avec Google et émet ses propres jetons OAuth
([ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md)). Il n'y a
plus de Supabase.

```
Application LifeOS ──(1) /connect/authorize──▶ LifeOS API ──(2)──▶ Google
        ▲                                         │   ◀──(3) /signin-google
        └───(4) code ─────────────────────────────┘
        └───(5) /connect/token : jeton d'accès + jeton de rafraîchissement
```

1. L'application ouvre `/connect/authorize` de l'API (code d'autorisation, PKCE S256).
2. L'API envoie vers Google si aucune connexion n'est en cours.
3. Google revient sur `/signin-google`. L'API vérifie que l'adresse est vérifiée et
   présente dans `Auth:AllowedEmails`, crée le compte à la première connexion, puis
   renvoie le code à l'application.
4. et 5. L'application échange le code contre ses jetons. Le jeton d'accès dure une
   heure ; le jeton de rafraîchissement tourne à chaque usage et expire après 60 jours
   sans utilisation.

## 1. Client OAuth Google (une fois par pile déployée)

Dans [Google Cloud Console](https://console.cloud.google.com/apis/credentials) :

1. **Écran de consentement OAuth** : type « Externe », statut « En production » (sinon
   les connexions expirent après 7 jours), portées `openid`, `email`, `profile`.
2. **Identifiants → Créer → ID client OAuth → Application Web** :
   - origines JavaScript autorisées : aucune ;
   - URI de redirection autorisée : `https://<adresse-de-l-api>/signin-google`.
3. Noter l'ID client et le code secret.

Le client Google de Supabase peut être réutilisé : il suffit d'y ajouter l'URI de
redirection ci-dessus. L'ancienne URI Supabase peut être retirée.

## 2. Clés de signature et de chiffrement des jetons

Deux secrets, générés une fois et conservés (les changer déconnecte tout le monde) :

```bash
# Clé de signature (RSA, PEM) : coller le contenu, retours à la ligne compris,
# ou remplacés par \n sur une seule ligne.
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048

# Clé de chiffrement (32 octets en base64)
openssl rand -base64 32
```

## 3. Variables du déploiement (`docker-compose.yaml`)

| Variable | Valeur |
| --- | --- |
| `LIFEOS_API_URL` | Adresse publique de l'API, sans `/api` (sert aussi d'émetteur des jetons) |
| `FRONTEND_URL` | Adresse de l'application web, sans `/` final |
| `GITHUB_PAGES_URL` | Facultatif : origine GitHub Pages ; l'application y est sous `/LifeOS/` |
| `AUTH_ALLOWED_EMAIL` | L'adresse Google autorisée |
| `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | Client OAuth Google (§ 1) |
| `AUTH_SIGNING_KEY`, `AUTH_ENCRYPTION_KEY` | Clés (§ 2) |

Les anciennes variables `SUPABASE_URL`, `VITE_SUPABASE_URL` et `VITE_SUPABASE_ANON_KEY`
ne servent plus.

L'API refuse de démarrer hors Development si le client Google ou les clés manquent.
Pour autoriser une autre adresse plus tard, ajouter `Auth__AllowedEmails__1`. Retirer
une adresse coupe son accès au plus tard une heure après, au prochain rafraîchissement.

## 4. Adresses de retour de l'application

Le client `lifeos-web` est enregistré au démarrage de l'API avec les adresses de
`Auth:WebClient:RedirectUris` : l'adresse web (`FRONTEND_URL` suivie de `/`), l'adresse
GitHub Pages et `lifeos://auth/callback` pour l'iPhone. Une adresse absente de la liste
est refusée par l'API.

## Développement local

- `appsettings.Development.json` autorise `http://localhost:4200/` ; les clés de jetons
  sont éphémères (une session ne survit pas au redémarrage de l'API).
- Sans `Auth__Google__ClientId`, l'API démarre mais la connexion est impossible.
  Pour tester la connexion, créer un client Google avec l'URI
  `http://localhost:5292/signin-google` et passer `Auth__Google__ClientId`,
  `Auth__Google__ClientSecret` et `Auth__AllowedEmails__0`.

## Dépannage

| Symptôme | Cause probable |
| --- | --- |
| Google affiche `redirect_uri_mismatch` | URI `/signin-google` absente du client Google, ou l'API se voit en `http` derrière le proxy |
| Retour sur la page de connexion avec « Ce compte Google n'est pas autorisé » | Adresse absente de `AUTH_ALLOWED_EMAIL`, ou adresse Google non vérifiée |
| Erreur `invalid_request` sur `redirect_uri` | L'adresse de l'application n'est pas dans `Auth:WebClient:RedirectUris` |
| « Session expirée » | Jeton de rafraîchissement inutilisé depuis 60 jours, ou clés changées |
