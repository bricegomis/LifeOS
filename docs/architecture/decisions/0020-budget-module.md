# ADR 0020 — Budget : budget mensuel, opérations bancaires et dépenses calculées

Statut : accepté, 2026-11-23. Étape 7 de la [roadmap](../../01-roadmap.md).
Maquette validée le 2026-11-23 (canevas des maquettes, « Budget — ordinateur » et
« Budget — smartphone ») ; module livré en code le même jour (schéma `budget`). Applique l'[ADR 0009](0009-modular-monolith-and-planning-core.md)
(module Budget, schéma `budget`), l'[ADR 0011](0011-conversational-assistant-mcp.md)
(outils de l'assistant) et l'[ADR 0016](0016-angular-material-ui-kit.md) (UI kit).

## Contexte

L'utilisateur tient un fichier Excel (« Slow Life ») qui estime ses dépenses par mois :

- un onglet **Récap** : une ligne par article (« Prêt immo », « Box atelier »,
  « Assurance voiture »…), rangée dans un poste (Prêt, Loyer, Assurance, Gazole,
  Alimentation, Santé, Abonnement…), avec un montant par mois **ou** par an ramené au
  mois, un repère « à diminuer » et le total mensuel et annuel ;
- des onglets de dépenses périodiques (hygiène, loisirs, vêtements) dont le prix est
  lissé au mois (un dentifrice tous les 3 mois) ;
- un calcul du gazole à partir des kilomètres.

Le fichier ne contient que du **prévu** : pas de dépenses réelles, pas de revenus.
L'alimentation y est budgétée « au doigt mouillé », ce qui est à l'origine de LifeOS.

Besoins exprimés :

- saisir le **réel** de chaque poste, voir si certains postes baissent ; d'autres
  sont fixes (loyer, crédit, abonnements) ;
- suivre l'évolution des dépenses **extra, non planifiées** ;
- des **enveloppes** mensuelles à tenir : travaux (X €/mois), sorties… ;
- un salaire à peu près fixe, qui varie avec les congés et RTT ;
- les compléments achetés pour 3 à 6 mois d'un coup, à lisser ;
- l'alimentation payée par carte, donc visible sur le compte, plutôt que par tickets.

Seul adulte du foyer : le budget porte sur ses dépenses et revenus uniquement.

## Décision

Le module Budget porte des **postes**, des **lignes prévues**, des **revenus prévus**,
des **opérations** et des **règles de classement**. Toutes les données sont dans le
schéma `budget`.

### Confidentialité

Le budget appartient à un compte, comme la santé (ADR 0018) : il porte le foyer et le
profil du compte, et un autre membre du foyer ne le voit pas. L'assistant y accède avec
les droits du compte connecté.

### Postes et lignes prévues

Un **poste** regroupe des lignes : Alimentation, Prêt, Loyer, Assurance, Carburant,
Santé, Abonnements, Travaux, Sorties… Il porte un nom et un ordre d'affichage. Des
postes sont proposés au départ, repris de l'Excel ; ils sont libres ensuite.

Une **ligne prévue** est l'équivalent d'une ligne du Récap. Elle porte :

- un nom et un poste ;
- un **type** :
  - **fixe** : un montant attendu (loyer, crédit, abonnement, assurance) ;
  - **enveloppe** : un plafond à ne pas dépasser (alimentation, travaux, sorties,
    carburant). Le non-dépensé n'est pas reporté : chaque mois repart du plafond ;
  - **extra** : aucun montant prévu ; la ligne sert à ranger et à suivre les
    dépenses non planifiées ;
  - **hors budget** : ni dépense ni revenu (virement vers un autre compte à soi,
    avance remboursée) ; ses opérations ne comptent nulle part ;
- un **montant** et sa **période** : par mois, par an ou tous les N mois. Le prévu
  mensuel est calculé (montant ÷ 12 par an, ÷ N tous les N mois) ;
- un repère **à diminuer** ;
- des notes.

Un changement de montant vaut à partir d'un mois choisi ; les mois passés gardent
l'ancien prévu. Une ligne peut être arrêtée (historique gardé).

Le calcul du gazole de l'Excel n'est pas repris : la ligne porte un montant, le réel
vient du compte.

### Revenus prévus

Une ligne de revenu (salaire, autre) porte un montant prévu par mois, modifiable mois
par mois (congés, RTT). Le reçu vient des opérations rangées dans cette ligne.

### Opérations

Une **opération** est un mouvement du compte : date, libellé, montant signé (dépense
négative, revenu positif), compte, et sa **ligne** (prévue, extra, hors budget ou
revenu), ou « non classée ». Elle vient :

- de l'**import d'un relevé OFX** exporté de la banque, chaque mois ou à n'importe
  quel rythme. Le format est figé sur un export BoursoBank réel (OFX 2.2 en XML) :
  compte (`ACCTID`), puis par opération la date de débit (`DTPOSTED`), le montant
  signé (`TRNAMT`), l'identifiant unique de la banque (`FITID`) et le libellé
  (`NAME`). `DTUSER` n'est pas fiable et n'est pas lu ; il n'y a ni catégorie ni
  commerçant séparé. Une opération déjà importée (même compte et même `FITID`) n'est
  jamais recréée : réimporter un mois ou des périodes qui se chevauchent est sans
  risque. Le solde du relevé est gardé pour l'affichage ;
- d'une **saisie à la main** (espèces, oubli).

**Date.** Un paiement par carte est débité un à trois jours après l'achat ; son
libellé commence par la date d'achat (« CARTE 29/10/26 … », « AVOIR 23/09/26 … »).
Cette date est reprise comme date de l'opération, la date de débit sinon : un achat
du 30 septembre compte en septembre. Ce découpage du libellé est propre au format
BoursoBank et testé sur lui.

**Remboursement.** Un avoir ou un remboursement rangé dans une ligne de dépense
vient en déduction du réel de cette ligne.

**Étalement.** Une opération peut couvrir N mois (compléments pour 6 mois, assurance
annuelle) : elle compte pour 1/N dans chacun des N mois à partir de son mois. Le mois
d'achat ne fait pas de pic.

Pas de découpage d'une opération en plusieurs lignes : un passage au supermarché
compte entier dans une ligne.

### Règles de classement

Une règle range les opérations importées : « libellé contient … » (sans tenir compte
des majuscules ni des accents, et facultativement un sens, dépense ou revenu) → une
ligne, avec un étalement facultatif. Les règles sont
écrites par l'utilisateur ou par l'assistant à sa demande, appliquées dans leur ordre à
l'import ; la première qui correspond gagne. Ce qui reste est « non classé » : rangé à
la main ou par Claude depuis l'assistant. L'API ne devine rien d'autre : pas de
correspondance approchée, pas de catégorie déduite ailleurs que des règles.

Ranger une opération à la main peut proposer de créer la règle correspondante.

Certaines opérations ne peuvent pas être rangées par une règle : un prélèvement
PayPal ou Amazon cache des achats de toute nature, un virement à une personne peut
être un loyer ou un remboursement. Elles restent non classées et sont rangées à la
main ou par Claude, qui peut s'aider des emails de confirmation de l'utilisateur.

### Mois

Pour un mois, calculé à la lecture :

- par poste et par ligne : prévu, réel (étalements compris), écart ; une enveloppe
  affiche son avancement et son dépassement ;
- revenus prévus et reçus ; reste du mois = revenus − dépenses, en prévu et en réel ;
- total des extras ;
- nombre d'opérations non classées.

### Évolution

Sur 6 mois, 12 mois ou depuis le début : réel par poste et par ligne, avec le prévu ;
les lignes « à diminuer » montrent leur tendance ; les extras ont leur propre courbe.

### Valeurs calculées par LifeOS

- **Compléments.** Une ligne prévue peut prendre comme prévu le coût mensuel des
  compléments actifs du compte, calculé par la Santé (cures lissées, ADR 0018) au lieu
  d'un montant saisi ; ce prévu suit les compléments actuels, pour tous les mois. Le réel vient des achats rangés dans cette ligne, étalés sur la durée
  couverte.
- **Alimentation.** Le prévu reste une enveloppe saisie et le réel vient des paiements
  par carte. Quand le semainier existera, le coût des repas planifiés (fourchette
  ADR 0010) pourra devenir le prévu de l'enveloppe ; à cadrer à l'étape 4.

Frontière (complète l'ADR 0009) : le Budget est un **module de synthèse**. Comme le
Planning, il peut lire les contrats publiés des modules de domaine, jamais leurs tables,
et aucun module ne le lit. Il ne lit la santé que du compte connecté, à qui appartient
aussi le budget. Le test d'architecture le vérifie.

### Mise en place depuis l'Excel

Pas d'import de fichier Excel : l'Excel ne contient que des lignes prévues. Claude les
crée depuis l'assistant à partir du fichier, puis l'utilisateur les relit.

### Écrans

Menu « Budget » dans la navigation, avec :

- **Mois** (page d'accueil du module) : choix du mois, reste du mois, postes avec
  prévu, réel et écart, enveloppes en barre d'avancement, extras, accès aux opérations
  non classées ;
- **Opérations** : liste avec recherche et filtres (mois, ligne, non classées), import
  d'un fichier OFX, saisie à la main, rangement et étalement ;
- **Lignes prévues** : postes et lignes, revenus, une page par ligne avec ses
  opérations et son évolution ;
- **Règles** : liste ordonnée, création et test sur les opérations existantes ;
- **Évolution** : graphiques par poste, extras, lignes à diminuer.

Selon les ADR 0015 et 0016 ; ordinateur et smartphone (l'import OFX se fait sur
ordinateur ou depuis les fichiers du téléphone).

### Assistant

Outils MCP en lecture et en écriture : postes, lignes prévues et revenus ; lire un mois
et l'évolution ; lister les opérations (dont les non classées), les ranger, les étaler,
en saisir ; créer et modifier des règles. L'import OFX se fait dans l'application.

## Conséquences

- Nouveau schéma `budget`. Les données financières sont personnelles : comme la
  santé, elles ne sont jamais envoyées à l'agent de planification.
- L'ADR 0009 gagne la notion de module de synthèse (Budget, Planning).
- Le contrat de la Santé expose le coût mensuel des compléments du compte.
- Les relevés importés ne sont pas conservés : seules les opérations le sont.
- Hors périmètre : import CSV, QIF ou Excel, connexion directe à la banque (agrégateur DSP2, à étudier plus
  tard), placements et épargne, plusieurs adultes, découpage d'une opération, liste de courses et stock.
