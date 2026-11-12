# ADR 0011 — Assistant conversationnel (MCP) et authentification propre

Statut : accepté, 2026-11-12. Cadrage de l'étape 1 bis de la
[roadmap](../../01-roadmap.md). Remplace l'[ADR 0002](0002-supabase-auth-only.md)
(Supabase pour l'authentification). Précise l'[ADR 0009](0009-modular-monolith-and-planning-core.md)
sur les acteurs IA ; ne change ni les modules ni les règles de l'agent de planification.

## Contexte

LifeOS stocke les informations de la vie du foyer. Les saisir à la main est trop
long : un aliment demande huit valeurs nutritionnelles et une image, un ticket demande
de recopier chaque prix. La base est vide et doit se remplir vite.

L'utilisateur veut décrire ce qu'il veut à Claude (claude.ai sur le web et l'iPhone,
Claude Code), et que Claude remplisse LifeOS : trouver les macros et une image,
créer l'aliment ou la recette, lire une photo de ticket et saisir les prix. Il veut
aussi que Claude lise LifeOS pour répondre à ses questions. Le même mécanisme doit
servir aux futurs modules (Sport, Tâches, Santé, Planning).

Seul un serveur MCP distant, authentifié par OAuth, est utilisable depuis claude.ai.
Des endpoints protégés par une clé fixe ne le sont pas.

## Décision

### 1. Deux acteurs IA distincts

| | Assistant conversationnel | Agent de planification (ADR 0009 § 7) |
| --- | --- | --- |
| Quand | Pendant une discussion, à la demande de l'utilisateur | Planifié, sans l'utilisateur |
| Identité | Le compte de l'utilisateur, par OAuth | Jeton d'agent du foyer |
| Lecture | Tout ce que le compte voit, santé de son profil comprise | Bibliothèques, règles, planning, signaux, résumés |
| Écriture | Directe dans les bibliothèques ; propositions pour le semainier | Propositions seulement |

L'assistant agit comme l'utilisateur, avec ses droits et pas plus : isolation par
foyer et santé par profil s'appliquent à lui comme à l'application. Ce qu'il lit est
envoyé au fournisseur d'IA de la discussion ; l'utilisateur l'accepte, santé comprise.
L'agent de planification garde les règles de l'ADR 0009, dont l'interdiction de lire
le dossier santé.

### 2. Un serveur MCP dans l'API

- Endpoint `/mcp` dans `LifeOS.Api` (transport HTTP, SDK officiel
  `ModelContextProtocol.AspNetCore`), dans le même conteneur. Pas de nouveau service.
- Chaque outil appelle un cas d'usage existant de la couche Application, comme un
  contrôleur. Validation, foyer, références et snapshots restent au même endroit.
- L'API n'appelle toujours aucun fournisseur d'IA et ne contient aucun prompt. Les
  descriptions d'outils sont de la documentation, pas des prompts.
- Les outils sont nommés par cas d'usage, pas en CRUD générique :
  `search_foods`, `create_food`, `set_food_image_from_url`, `create_recipe`,
  `list_pending_receipts`, `get_receipt_photo`, `add_price`, etc. Chaque module ajoute
  ses outils quand il est livré, dans son propre dossier.
- Les outils de création renvoient l'identifiant et l'objet créé. Les outils de
  recherche existent pour que l'assistant vérifie l'existant avant de créer.

### 3. Écriture directe ou proposition

- **Écriture directe** pour les bibliothèques et le référentiel : aliments, recettes,
  magasins, produits, prix, avis, séances types, tâches, mesures de santé. L'assistant
  montre ce qu'il va créer dans la discussion ; l'utilisateur valide là.
- **Proposition** pour le semainier, comme l'ADR 0009 § 6 : l'assistant crée une
  proposition, l'utilisateur l'accepte dans l'application. Le détail sera fixé à
  l'étape 4.

### 4. Enrichissement par l'assistant, jamais par l'API

L'API reste sans enrichissement externe et sans rapprochement par le nom. Ce travail
est fait par l'assistant, sous le contrôle de l'utilisateur :

- **Nutrition** : l'assistant cherche les valeurs dans une source fiable (Ciqual en
  priorité pour la France), cite sa source dans la discussion et saisit les valeurs.
  Elles sont stockées comme des valeurs manuelles ; une valeur inconnue reste nulle.
- **Images** : l'assistant fournit une URL ; l'API télécharge l'image et applique les
  mêmes règles qu'un envoi depuis l'application (type, taille, volume
  `lifeos_food_images`). Le téléchargement n'accepte que `https`, refuse les adresses
  privées ou locales et a un délai et une taille maximum.
- **Tickets** : la photo est prise dans l'application, comme aujourd'hui. L'outil
  `get_receipt_photo` renvoie l'image à l'assistant, qui la lit, propose pour chaque
  ligne le produit correspondant, et saisit les prix confirmés par l'utilisateur.
  L'API ne lit toujours rien dans l'image.

### 5. Authentification : LifeOS devient son propre serveur OAuth

Supabase est retiré : l'API authentifie l'utilisateur et émet ses jetons.

- **Connexion** : avec un compte Google, géré directement par l'API (OpenID Connect).
  Seules les adresses listées dans la configuration (`Auth:AllowedEmails`) peuvent se
  connecter ; la première connexion crée le compte et le foyer, comme aujourd'hui.
  Lien magique et passkey ne sont pas repris ; une passkey pourra s'ajouter plus tard.
- **Compte** : une table `users` (identifiant Google, e-mail, nom) remplace
  l'identifiant Supabase. `household_members` référence `users`.
- **Serveur d'autorisation** : OAuth 2.1, code d'autorisation avec PKCE S256 seulement,
  jetons d'accès courts, jetons de rafraîchissement à rotation, révocation. Les
  échanges de jetons reposent sur une bibliothèque éprouvée (OpenIddict), pas sur du
  code maison.
- **Clients enregistrés d'avance**, pas d'enregistrement dynamique :
  - `lifeos-web` : l'application Angular (web et iPhone, retour par
    `lifeos://auth/callback`), sans écran de consentement ;
  - Claude Code : par son document de métadonnées client (CIMD), avec retour local
    `http://localhost` ou `http://127.0.0.1` sur n'importe quel port ;
  - claude.ai (web, bureau, iPhone) : par son CIMD s'il en présente un, sinon par un
    identifiant client saisi dans les réglages avancés du connecteur ; retour
    `https://claude.ai/api/mcp/auth_callback`.
- **Découverte** : `/mcp` répond `401` avec un pointeur vers les métadonnées de
  ressource protégée (RFC 9728) ; l'API publie ses métadonnées de serveur
  d'autorisation (RFC 8414).
- **Consentement** : la première connexion d'un client Claude affiche « Autoriser
  Claude à accéder à LifeOS » avec le nom d'hôte du retour. Les Réglages listent les
  accès accordés, révocables.
- **Portée** : une seule portée, l'accès complet du compte (§ 1). Les jetons d'agent de
  l'étape 4 seront distincts.

## Conséquences

- La base des comptes est remise à zéro : foyers et membres sont recréés à la
  première connexion (accord de l'utilisateur, 2026-11-12). Migration destructive.
- `@supabase/supabase-js`, la configuration Supabase et le dossier `supabase/`
  disparaissent. La page de connexion n'a plus qu'un bouton Google.
- L'utilisateur crée un client OAuth Google (adresse de retour sur l'API) et ajoute
  un connecteur Claude, par pile déployée.
- Les écrans de connexion, de consentement et des accès accordés suivent le style
  existant, sans maquette (accord de l'utilisateur, 2026-11-12).
- Le code Swift n'est pas touché : le retour `lifeos://auth/callback` existe déjà.

## Alternatives écartées

- **Endpoints REST avec une clé secrète** : inutilisables depuis claude.ai, et la
  clé finirait dans les discussions.
- **Serveur MCP séparé** devant l'API : un service de plus, et la tentation de
  dupliquer les règles hors de la couche Application.
- **Garder Supabase** comme serveur OAuth : un tiers de plus à gérer, que
  l'utilisateur veut retirer.
- **Enregistrement dynamique (DCR)** : ouvre l'enregistrement à n'importe quel client
  et accumule les clients ; inutile pour un foyer.
- **OCR ou appel à un modèle de vision dans l'API** : contraire à l'ADR 0009 et
  inutile, l'assistant lit la photo.
