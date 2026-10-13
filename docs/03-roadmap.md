# LifeOS — Livraison aliments et tickets

Le semainier manuel alimentation/sport est conservé. Le lot approuvé du
11 octobre 2026 remplace le catalogue unifié, les parcours stock/courses et
le frontend Vue ; [périmètre](01-scope-and-versions.md), [vocabulaire](02-core-model.md).

| Lot | Livrable |
| --- | --- |
| Modèle | Aliments génériques avec complétion manuelle ; produits commerciaux explicitement liés ; migration transactionnelle additive depuis UnifiedProducts. |
| Consommateurs | Recettes et nouvelles lignes de repas sur aliments ; snapshots inchangés ; coûts non inventés ; réutilisation sans dépendance au stock. |
| Tickets | Source brute, lignes persistantes ordonnées, corrections et contexte magasin/date ; validation individuelle transactionnelle, achats/prix et idempotence. |
| Angular | Aliments au premier plan ; produits/prix secondaires ; Tickets import/liste/détail reprenable ; dialogues natifs et mobile sans table horizontale. |
| Suppressions | Routes/UI/API/DTO/ports/repositories/DI stock et courses ; catalogues alimentaires parallèles et OFF actifs ; Vue et ses dépendances propres. |
| Livraison | Tests domaine/API PostgreSQL, migration vierge/upgrade/rollback/isolation/reprise/concurrence, contrôles Angular et inspection desktop/mobile ; workflows Angular/API. |

## Publication et autorisations

Les merges automatiques sont explicitement autorisés **après** contrôles et
traitement des revues, sans contourner les protections ou ignorer CI rouge.
Surveiller la publication des deux images API/Angular du commit fusionné avant
de signaler qu'elles sont disponibles. Le refresh des containers est effectué par
l'utilisateur ; sauvegarde PostgreSQL préalable nécessaire pour la migration au
démarrage API. Retour arrière par restauration, pas par suppression de données.

Les validations locales utilisent une authentification synthétique en environnement
Testing et PostgreSQL éphémère. Elles ne prouvent ni OAuth réel ni fonctionnement
du VPS après refresh. Aucune intervention manuelle en production n'est autorisée.

## Hors livraison

OCR/IA, enrichissement santé, rapprochement par nom, choix de marque optimisé,
stock/courses, recommandations médicales, connecteurs sportifs et refonte du
planning restent exclus. Toute évolution future exige un besoin produit explicite.

## iPhone personnel — priorités approuvées

Le lot iOS prépare l'installation directe Xcode **entièrement gratuite**, sans
abonnement Apple Developer, et conserve les widgets WidgetKit réels.
Il ne réintroduit ni stock/courses, ni catalogue concurrent des aliments, ni
enrichissement automatique. [Guide, preuves et étapes humaines](ios-v1.md).
**Aucun merge, publication ou changement de production n'est autorisé pour ce lot** ;
les autorisations générales de la livraison aliments/tickets ci-dessus ne
constituent pas une autorisation de déploiement iOS.

Les corrections de l'existant (contrat cuisine, purge, concurrence, expiration,
pont, callbacks, compilation et guide) appartiennent à ce lot.
Les fonctionnalités nouvelles ci-dessous sont **à réaliser ultérieurement** :
le snapshot widget actuel ne prouve pas une consultation Angular hors connexion.

### P0 — installation gratuite, widgets et isolation fiables

**Bénéfice :** utiliser LifeOS sur son propre iPhone sans paiement, sans perdre
une case cuisine ou exposer les données du compte précédent.
**Périmètre :** app + extension signées par la même Personal Team, IDs/App Group
personnalisables, contrats natifs versionnés, mutations coordonnées, purge après
déconnexion/changement d'identité, reprise au premier plan et erreurs explicites.
Les défauts observés sont corrigés dans le lot préparatoire ; la preuve appareil
reste une étape humaine distincte, non déclarée accomplie par un build unsigned.

**Critères :** installer les deux cibles gratuitement ; ajouter et actualiser un
vrai widget après modification repas/sport ; alterner actions cuisine app/widget
sans écrasement ; aucune restauration/réécriture d'ancienne identité après logout,
y compris pendant une sync ; session expirée refusée ; renouvellement Xcode
reproductible au bout de 7 jours ; documenter toute erreur réelle de provisioning.
**Dépendances :** Mac/Xcode cohérent, iPhone iOS 17+, compte Apple gratuit choisi
par l'utilisateur, App Group identique, config publique HTTPS, CORS exact et
redirections Supabase vérifiées.
**Limites :** profils 7 jours et quotas Personal Team, pas d'installation gratuite
permanente ; cadence widgets décidée par iOS, pas de garantie avant signature réelle.
Pas de remplacement par widget statique/raccourci/endpoint public en cas de refus.

### P1 — consultation du planning hors connexion

**Bénéfice :** lire ses repas et séances sans réseau dans l'app, pas seulement
dans le widget.
**Périmètre futur :** cache de lecture des semaines explicitement synchronisées,
isolé utilisateur/foyer, date de fraîcheur visible et avertissement hors connexion ;
vider le cache au logout/changement d'identité. Pas de queue d'écriture ni fusion
automatique, pas d'invention de semaines vides ou d'heures.
**Critères :** une semaine déjà chargée reste consultable en mode avion/après
redémarrage ; semaine absente expliquée ; données périmées identifiables ; aucune
donnée d'un autre compte ; retour réseau met à jour sans modifier silencieusement
les repas, portions, snapshots historiques ou séances.
**Dépendances :** P0, stratégie de stockage/version/migration du cache API,
tests réseau/interruption/isolation et règles de fraîcheur explicites.
**Limites :** stockage local supprimable par iOS, aucune donnée jamais chargée
disponible ; Supabase reste auth-only et l'API source de vérité.
**Statut :** roadmap seulement, non livré dans ce lot.

### P1 — reprise cuisine après fermeture et redémarrage

**Bénéfice :** reprendre la préparation sans chercher où l'on s'était arrêté.
**Périmètre futur :** parcours visible de reprise d'une session encore valide,
réconciliation app/widget/Live Activity après arrêt brutal ou reboot, message
explicite si expirée/remplacée ; reprise sur cet appareil uniquement.
La lecture/migration/expiration et la mutation partagée de l'existant sont corrigées
en P0 ; cela ne constitue pas encore un parcours de reprise validé sur iPhone.
**Critères :** étapes, date de début et révision conservées à la fermeture ;
actions des widgets récupérées sans mise à jour perdue ; pas de reprise d'une
session ancienne/d'un autre compte ; aucune nouvelle durée de 8 h accordée
par un simple retour au premier plan.
**Dépendances :** P0, preuve signature/IO App Group, scénarios appareil arrêt
forcé/redémarrage, autorisation et état ActivityKit.
**Limites :** iOS peut terminer une Live Activity ; la checklist peut survivre
sans activité, seulement avant expiration. Pas de synchronisation multi-appareils.
**Statut :** fonctionnalités de reprise à inscrire seulement, non ajoutées ici.

### P1 — diagnostics d'installation et récupération compréhensibles

**Bénéfice :** distinguer une erreur de config, une permission refusée, une panne
réseau et une sync incomplète sans devoir lire un log technique.
**Périmètre futur :** diagnostics guidés de config publique, pont/signature App Group,
permissions rappels/Live Activities et dernière sync/fraîcheur, avec action
concrète de récupération. Les erreurs essentielles et le statut du pont existants
sont rendus visibles en P0, pas un nouveau centre de diagnostic dans ce lot.
**Critères :** messages distincts web/non disponible/erreur natif ; boutons
réessayer/ouvrir les réglages appropriés ; aucun faux succès quand la sync ou la
permission échoue ; aucune clé/token donnée de foyer dans un rapport exporté ;
instructions hebdomadaires explicites.
**Dépendances :** P0, taxonomie d'erreurs, parcours humain Xcode/iPhone, copie UX
et tests permissions refusées/config absente/offline.
**Limites :** l'app ne signe pas elle-même son extension, ne se connecte pas au
compte Apple à la place de l'utilisateur et ne peut pas forcer WidgetKit à rafraîchir.
**Statut :** roadmap seulement. Scan caméra, HealthKit et push distants hors lot.
