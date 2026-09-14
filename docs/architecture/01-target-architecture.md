# LifeOS — Architecture cible

Ce document décrit l'architecture technique cible telle que validée après
l'audit (`docs/architecture/00-technical-audit.md`). Les décisions
structurantes sont détaillées dans les ADR du dossier
`docs/architecture/decisions/`.

## Vue d'ensemble

```
Vue 3 (frontend)  --HTTPS/JWT-->  API ASP.NET Core  --EF Core/Npgsql-->  PostgreSQL (Coolify)
       |                                  ^
       | (login / magic link / JWT)       | validation JWT
       v                                  |
   Supabase Auth  -------------------------
```

- **Vue** reste l'unique client. Elle ne parle qu'à l'API .NET pour toute
  opération métier ; elle ne parle jamais directement à PostgreSQL ni à
  Supabase pour des données métier.
- **Supabase** est utilisé **uniquement** pour l'authentification (magic
  link, sessions, émission de JWT). Aucune donnée métier nouvelle n'est
  stockée dans Supabase.
- **L'API ASP.NET Core** est le point d'accès unique aux données métier. Elle
  valide les JWT Supabase, résout l'utilisateur vers son foyer, exécute les
  calculs déterministes (génération de semaine, portions, etc.) et persiste
  via EF Core / Npgsql dans PostgreSQL.
- **PostgreSQL**, auto-hébergé sur Coolify, est la source de vérité
  métier. `localStorage` ne redevient qu'un cache frontend optionnel, jamais
  une source de vérité.

## Responsabilités par composant

### Frontend (Vue 3 + TypeScript)

- Affiche et édite les données via l'API.
- Gère l'état de session (JWT Supabase) et l'attache à chaque appel API.
- Peut mettre en cache localement pour la réactivité perçue, mais toute
  donnée locale doit pouvoir être invalidée/recalculée depuis l'API.
- Ne réalise plus de calcul métier déterminant (génération de semaine,
  portions, agrégations nutritionnelles) : ces calculs sont déplacés côté
  API au fil des jalons de la roadmap technique.

### API ASP.NET Core

- Point d'entrée HTTP unique pour toute opération métier.
- Valide les JWT Supabase (signature, expiration, issuer) sur chaque requête
  authentifiée.
- Résout l'identité (`sub` Supabase) vers un `household_id` (voir
  « Résolution utilisateur → foyer » ci-dessous).
- Applique l'isolation par foyer sur toutes les requêtes de données.
- Héberge les calculs déterministes (génération de semaine, portions,
  nutrition) au fur et à mesure de leur portage depuis le frontend.
- Persiste via EF Core / Npgsql, avec des migrations EF Core versionnées dans
  le dépôt.
- Intègre Open Food Facts à la demande (recherche produit), avec cache local
  des résultats et tolérance de mode dégradé si l'API externe est
  indisponible.

### PostgreSQL (Coolify)

- Source de vérité unique pour toutes les données métier (foyers, membres,
  articles, recettes, plannings, stock, courses, magasins, prix, etc.).
- Relations fortes (clés étrangères, contraintes) pour le cœur du modèle.
- `jsonb` réservé aux données intrinsèquement flexibles : métadonnées libres
  de recette, payload brut Open Food Facts, explications/scénarios de
  suggestion, snapshots. Le `jsonb` ne remplace pas les relations du modèle
  central.

### Supabase

- Authentification uniquement : inscription/connexion par magic link,
  gestion de session, émission et renouvellement des JWT.
- Aucune table métier nouvelle n'est ajoutée dans Supabase. Les tables
  historiques de la migration `20260707_0001_lifeos_phase1.sql` sont
  amenées à être migrées vers PostgreSQL au fil de la roadmap technique
  (une migration de données explicite sera nécessaire, pas traitée dans ce
  document).

## Flux d'authentification (JWT)

1. L'utilisateur se connecte via Supabase Auth (magic link) depuis le
   frontend.
2. Supabase émet un JWT signé, stocké côté frontend et attaché à chaque appel
   API (`Authorization: Bearer <jwt>`).
3. L'API valide la signature et les claims du JWT (déjà en place aujourd'hui
   via OIDC).
4. L'API extrait le `sub` (identifiant utilisateur Supabase) du JWT validé.
5. L'API résout ce `sub` vers un `household_id` (voir ci-dessous) et l'utilise
   pour toute requête de données ultérieure dans la requête HTTP en cours.

## Résolution utilisateur → foyer

- Chaque utilisateur Supabase (`sub`) est associé à exactement un foyer
  propriétaire au MVP, via une table `household_members` (voir
  `docs/architecture/02-data-model.md`).
- Cette résolution se fait côté API, à chaque requête authentifiée, avant
  toute lecture/écriture de données métier.
- Le modèle prévoit dès le départ la possibilité de plusieurs membres par
  foyer (rôles, invitations), même si le MVP n'expose qu'un scénario
  mono-utilisateur par foyer.

## Défense en profondeur

- **Authentification** : validation JWT à la frontière de l'API (couche déjà
  en place).
- **Autorisation par foyer** : chaque requête de données est filtrée par
  `household_id` résolu côté serveur, jamais transmis tel quel par le
  client. Le client ne peut pas usurper un autre foyer en modifiant un
  paramètre de requête.
- **Contraintes en base** : clés étrangères vers `household_id` sur les
  agrégats racines (voir `docs/architecture/02-data-model.md`), pour éviter
  les incohérences même en cas de bug applicatif.
- **Secrets hors dépôt** : la chaîne de connexion PostgreSQL
  (`DATABASE_URL` / connection string) n'est jamais committée. Elle est
  fournie via variables d'environnement / secrets Coolify, en dehors du
  dépôt Git, y compris pour les environnements de développement (fichier
  `.env` non versionné, à l'image de `src/frontend/.env.example`).
- **Réseau** : l'API et PostgreSQL communiquent sur un réseau privé côté
  Coolify (à confirmer précisément selon les capacités réelles de Coolify,
  voir la note de prudence dans l'ADR 0001).

## Déploiement (vue d'ensemble)

- **PostgreSQL** : instance auto-hébergée sur Coolify, avec volume
  persistant. Sauvegardes et procédure de restauration à définir et tester
  (voir ADR 0001) — les modalités précises restent à confirmer selon
  l'offre Coolify effectivement disponible.
- **API .NET** : conteneurisée, exposée uniquement en réseau privé vis-à-vis
  de PostgreSQL, exposée publiquement (HTTPS) pour le frontend.
- **Frontend Vue** : conteneurisé ou servi statiquement, avec TLS géré par
  Coolify.
- **Supabase** : reste un service externe séparé, utilisé uniquement pour
  l'authentification.

Ce paragraphe décrit une intention de déploiement ; les détails précis
d'exploitation Coolify (sauvegardes automatiques, monitoring, mise à jour)
sont à confirmer au moment de la mise en place réelle et ne doivent pas être
considérés comme déjà garantis par la plateforme.
