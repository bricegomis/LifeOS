# ADR 0002 — Supabase conservé uniquement pour l'authentification

## Statut

Accepted

## Contexte

Supabase est utilisé aujourd'hui à la fois pour l'authentification (magic
link, sessions, JWT) et pour une persistance distante partielle de certaines
données métier (`user_settings`, `planning_rules`, `week_context_config`,
`week_mode_overrides`, `week_plans`, cf. audit technique). Avec l'introduction
de PostgreSQL auto-hébergé sur Coolify comme base métier (ADR 0001), le rôle
de Supabase doit être clarifié pour éviter une double source de vérité.

## Décision

Supabase est conservé **uniquement** pour l'authentification : magic link,
gestion de session, émission et validation des JWT. Aucune nouvelle donnée
métier n'est ajoutée à Supabase. La base métier (PostgreSQL sur Coolify) ne
doit dépendre d'aucune manière du fonctionnement de Supabase pour son
fonctionnement (hormis pour l'obtention initiale de l'identité de
l'utilisateur au moment de la connexion).

L'API ASP.NET Core continue de valider les JWT émis par Supabase (JWKS /
OIDC), comme c'est déjà le cas aujourd'hui, sans implémenter de flux de
connexion propre à l'API.

## Alternatives rejetées

- **Étendre Supabase comme base métier complète** : rejeté par l'ADR 0001,
  pour garder une base métier auto-hébergée et indépendante d'un fournisseur
  tiers.
- **Remplacer Supabase Auth par un système d'authentification maison** :
  rejeté car Supabase Auth fonctionne déjà correctement pour le besoin
  actuel (magic link, JWT) et une réimplémentation ajouterait un risque de
  sécurité et un coût de maintenance sans bénéfice produit identifié.
- **Utiliser Supabase Row Level Security (RLS) comme mécanisme d'isolation
  des données métier** : sans objet dès lors que les données métier ne
  résident plus dans Supabase ; voir ADR 0003 pour le mécanisme d'isolation
  retenu côté PostgreSQL/API.

## Conséquences

- Les tables métier historiques dans Supabase (`user_settings`,
  `planning_rules`, `week_context_config`, `week_mode_overrides`,
  `week_plans`) sont amenées à être migrées vers PostgreSQL au fil de la
  roadmap technique ; elles ne reçoivent plus de nouvelles fonctionnalités
  une fois leur équivalent porté côté PostgreSQL.
- L'API doit continuer à valider les JWT Supabase à chaque requête
  authentifiée (mécanisme déjà en place, inchangé par cette décision).
- Toute nouvelle fonctionnalité métier (recettes, planning, stock, courses,
  magasins, prix, scénarios) est persistée exclusivement dans PostgreSQL,
  jamais dans Supabase.
- La disponibilité de la base métier ne doit pas dépendre de Supabase :
  seule la capacité à se (re)connecter (obtenir/valider un JWT) en dépend.

## Conditions de réévaluation

Cette décision doit être réévaluée si :
- Supabase Auth devient indisponible ou insuffisant pour les besoins
  d'authentification du produit (par exemple, besoin de SSO d'entreprise,
  de gestion fine de rôles au niveau plateforme) ;
- le coût ou les contraintes de Supabase Auth deviennent disproportionnés
  par rapport à l'usage réel du produit.
