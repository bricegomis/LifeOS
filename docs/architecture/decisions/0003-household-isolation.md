# ADR 0003 — Isolation des données par foyer (household)

## Statut

Accepted

## Contexte

L'isolation actuelle des données backend se fait par `ownerId` / `sub`
(l'identifiant utilisateur Supabase), c'est-à-dire par utilisateur individuel
(cf. audit technique). Or la vision produit
(`docs/00-vision.md`) et le cadrage fonctionnel
(`docs/01-scope-and-versions.md`) définissent le **foyer** comme le
périmètre isolé qui possède toutes les données métier, avec un propriétaire
au MVP et une évolution prévue vers plusieurs membres et invitations.

Le passage à PostgreSQL comme base métier (ADR 0001) est l'occasion de
corriger ce décalage dès le premier jalon plutôt que de le reporter.

## Décision

L'isolation des données métier se fait par `household_id` dès le premier
jalon technique, et non par identifiant utilisateur individuel. Une table
`household_members` associe un ou plusieurs utilisateurs Supabase
(`supabase_user_id`) à un foyer, avec un rôle (`owner` au MVP). Tous les
agrégats racines du modèle de données
(`docs/architecture/02-data-model.md`) portent une colonne `household_id`.

L'isolation est **applicative et systématique** : l'API résout le
`household_id` à partir du JWT Supabase validé à chaque requête, et l'utilise
pour filtrer toutes les lectures et écritures. Ce filtrage n'est pas laissé à
l'initiative du client (le client ne transmet jamais directement un
`household_id` de confiance).

Row Level Security (RLS) PostgreSQL native **n'est pas retenue** comme
mécanisme de défense supplémentaire dans l'immédiat : PostgreSQL est
désormais une base séparée de Supabase (ADR 0001, ADR 0002), et l'isolation
applicative au niveau de l'API constitue la ligne de défense principale.
L'introduction de RLS pourra être reconsidérée plus tard comme couche de
défense en profondeur additionnelle, sans qu'elle soit une dépendance de
cette décision.

Le MVP conserve un modèle simple : un utilisateur possède un foyer en tant
que propriétaire (`owner`). Les membres additionnels et les invitations sont
préparés dans le modèle (table `household_members`, colonne `role`) mais ne
sont pas exposés comme fonctionnalité utilisateur au MVP.

## Alternatives rejetées

- **Conserver l'isolation par utilisateur individuel** : rejeté car
  incompatible avec la vision produit du foyer comme périmètre de données,
  et car cela nécessiterait une migration structurante plus tard, à un
  moment où davantage de domaines et de données existeraient déjà.
- **S'appuyer sur PostgreSQL RLS comme mécanisme principal d'isolation** :
  rejeté pour l'instant, car cela ajouterait une complexité de configuration
  (rôles PostgreSQL, policies) pour un bénéfice marginal tant que l'unique
  point d'accès aux données est l'API elle-même (pas d'accès direct
  multi-tenant à la base par des clients tiers).

## Conséquences

- Toute nouvelle entité de données métier doit inclure une colonne
  `household_id` (directe ou via une relation vers une entité qui la porte)
  et toute requête d'application doit filtrer explicitement dessus.
- L'API doit implémenter la résolution `supabase_user_id` → `household_id`
  comme une étape systématique tôt dans le traitement de chaque requête
  authentifiée (voir `docs/architecture/01-target-architecture.md`).
- Les contraintes de clé étrangère vers `households` sur les agrégats
  racines fournissent une garantie de cohérence même en cas de bug
  applicatif (une ligne orpheline sans foyer valide est rejetée par la base).
- Le modèle reste extensible vers plusieurs membres par foyer sans migration
  de schéma supplémentaire : il suffira d'exposer des fonctionnalités
  d'invitation et de gestion de rôle au-dessus de `household_members`.

## Conditions de réévaluation

Cette décision doit être réévaluée si :
- un besoin réel d'accès direct multi-tenant à PostgreSQL par des clients
  autres que l'API apparaît (auquel cas RLS redeviendrait pertinent comme
  défense en profondeur) ;
- le modèle « un foyer = un propriétaire » devient limitant une fois la
  collaboration multi-membres effectivement demandée par les utilisateurs,
  nécessitant une évolution des règles de rôle et de permission au-delà de
  ce que `household_members` prévoit aujourd'hui.
