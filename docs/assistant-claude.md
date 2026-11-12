# Assistant Claude

Claude lit et remplit LifeOS pendant une discussion, avec les droits de ton compte
([ADR 0011](architecture/decisions/0011-conversational-assistant-mcp.md)). L'adresse à
connecter est celle de l'API suivie de `/mcp`, par exemple
`https://api-lifeos.mondomaine.fr/mcp`. Elle est affichée, avec un bouton Copier,
dans LifeOS → Réglages → Assistants connectés.

## claude.ai (web, application iPhone et bureau)

1. Sur claude.ai : **Paramètres → Connecteurs → Ajouter un connecteur personnalisé**.
2. Nom : `LifeOS` ; URL : l'adresse `/mcp`. Ne rien mettre dans les réglages avancés.
3. **Se connecter** : une fenêtre s'ouvre sur LifeOS. Connexion Google si besoin, puis
   l'écran « Autoriser Claude à accéder à LifeOS ? » : **Autoriser**.
4. Le connecteur suit ton compte claude.ai : il est aussi disponible dans l'application
   iPhone. Dans une discussion, l'activer depuis le menu des outils.

## Claude Code

```bash
claude mcp add --transport http lifeos https://api-lifeos.mondomaine.fr/mcp
```

Puis, dans une session, `/mcp` → `lifeos` → **Authenticate**. Le navigateur affiche le
même écran d'autorisation, avec un avertissement : le retour se fait vers un programme
de cet ordinateur. N'autoriser que si tu viens de lancer la commande.

## Vérifier

Demander à Claude : « Quels sont les profils de mon foyer LifeOS ? ». Il appelle l'outil
`get_household` et liste les profils.

## Retirer un accès

LifeOS → Réglages → Assistants connectés → **Retirer l'accès**. Les jetons de cet
assistant cessent de fonctionner ; il devra redemander l'autorisation.

## Ce que fait l'assistant

| Outil | Rôle | Disponible |
| --- | --- | --- |
| `get_household` | Foyer et profils | oui |
| Aliments, recettes, magasins, produits, prix, images | Recherche et création | lot 3 |
| Tickets en attente, photo, saisie des prix | Traitement des tickets | lot 4 |

## Dépannage

| Symptôme | Cause probable |
| --- | --- |
| « Couldn't reach the MCP server » | Adresse sans `/mcp`, ou API injoignable depuis Internet |
| Erreur `invalid_client` après le clic sur Se connecter | Client Claude inconnu : son document de métadonnées n'a pas pu être lu (journaux de l'API, « Client metadata document ») |
| Retour en boucle sur l'écran de connexion | Adresse Google non autorisée (`AUTH_ALLOWED_EMAIL`) |
