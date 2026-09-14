# ADR 0001 — PostgreSQL auto-hébergé sur Coolify comme base métier

## Statut

Accepted

## Contexte

L'audit technique (`docs/architecture/00-technical-audit.md`) a établi que le
backend actuel (`LifeOS.Api`) ne persiste rien durablement : toutes les
répertoires d'infrastructure sont en mémoire et réamorcées à chaque
redémarrage. La roadmap technique nécessite une vraie base de données pour
porter des domaines complets (foyer, recettes, planning, stock, courses,
etc.) au-delà du jalon 1.

Le projet dispose déjà d'un projet Supabase, utilisé aujourd'hui pour
l'authentification et une persistance distante partielle
(`user_settings`, `planning_rules`, `week_context_config`,
`week_mode_overrides`, `week_plans`). Un futur serveur Coolify est prévu pour
héberger les services applicatifs du projet.

## Décision

PostgreSQL, auto-hébergé sur le futur serveur Coolify, devient la base de
données métier principale et la source de vérité pour l'ensemble du domaine
LifeOS (foyers, membres, articles, recettes, planning, stock, courses,
magasins, prix, scénarios). L'API ASP.NET Core y accède via EF Core /
Npgsql, avec des migrations EF Core versionnées dans le dépôt.

## Alternatives rejetées

- **Continuer avec Supabase Postgres comme base métier** : rejeté pour
  garder une séparation nette entre l'authentification (Supabase) et les
  données métier (voir ADR 0002), et pour ne pas dépendre d'un fournisseur
  tiers pour la donnée la plus sensible du produit.
- **Base documentaire (DocumentDB / MongoDB-like)** : discutée mais non
  retenue. Le modèle de données (`docs/architecture/02-data-model.md`)
  comporte des relations fortes (foyer, membres, recettes, planning) qui se
  prêtent mieux à un modèle relationnel ; `jsonb` dans PostgreSQL couvre déjà
  les besoins de flexibilité identifiés (métadonnées de recette, payload
  Open Food Facts, explications de scénario).
- **Rester en mémoire plus longtemps** : rejeté car incompatible avec la
  nécessité de conserver les foyers, l'historique des semaines et les
  données de courses/stock au-delà d'un redémarrage de processus.

## Conséquences

- L'API doit introduire EF Core, un `DbContext`, et un pipeline de
  migrations versionnées dès le premier domaine porté en persistance réelle
  (jalon « fondations foyer + PostgreSQL + EF Core »).
- La chaîne de connexion (`DATABASE_URL` / connection string) est un secret
  qui ne doit jamais être committé ; elle est fournie par variable
  d'environnement / secret Coolify, en dehors du dépôt.
- L'auto-hébergement sur Coolify implique explicitement, en tant que
  responsabilité du projet et non d'un fournisseur managé :
  - des **sauvegardes** régulières du volume PostgreSQL ;
  - une **procédure de restauration testée**, pas seulement des sauvegardes
    passives ;
  - des **mises à jour** de version PostgreSQL planifiées ;
  - un **monitoring** basique (disponibilité, espace disque) ;
  - un **stockage persistant** (volume) correctement configuré côté
    Coolify ;
  - la **terminaison TLS et l'isolation réseau** entre l'API et la base
    (idéalement réseau privé Coolify).
- Les capacités précises offertes nativement par Coolify (sauvegardes
  automatiques, monitoring intégré, etc.) ne sont pas supposées acquises par
  ce document : elles doivent être **confirmées** au moment de la mise en
  place réelle du serveur, et documentées dans un futur guide de
  déploiement une fois vérifiées.
- Les données actuellement dans les tables Supabase historiques
  (`user_settings`, `planning_rules`, `week_context_config`,
  `week_mode_overrides`, `week_plans`) devront faire l'objet d'une migration
  explicite vers PostgreSQL à un jalon ultérieur ; ce document ne définit
  pas cette migration de données.

## Conditions de réévaluation

Cette décision doit être réévaluée si :
- le serveur Coolify prévu ne peut pas garantir un volume persistant fiable
  ou des sauvegardes exploitables ;
- la charge opérationnelle de l'auto-hébergement (sauvegardes, mises à jour,
  monitoring) s'avère disproportionnée par rapport aux ressources
  disponibles pour maintenir le projet ;
- un besoin de scalabilité ou de haute disponibilité dépassant les capacités
  d'une instance auto-hébergée unique apparaît.
