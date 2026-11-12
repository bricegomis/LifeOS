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

Claude agit directement dans les bibliothèques (ADR 0011, § 3) : il montre ce qu'il va
créer, tu valides dans la discussion. Il cherche lui-même les valeurs nutritionnelles
(Ciqual en priorité) et les images ; LifeOS ne va jamais chercher ces données seul.

| Domaine | Outils |
| --- | --- |
| Foyer | `get_household` (profils, pour les avis) |
| Aliments | `search_foods`, `get_food` (avec produits et avis), `create_food`, `update_food` (remplace toute la fiche), `archive_food`, `set_food_image_from_url`, `remove_food_image` |
| Recettes | `search_recipes`, `get_recipe` (ingrédients, nutrition par portion, coût min–max, avis), `create_recipe` (avec ses ingrédients), `update_recipe`, `add_recipe_ingredient`, `update_recipe_ingredient`, `remove_recipe_ingredient`, `archive_recipe`, `set_recipe_image_from_url`, `remove_recipe_image` |
| Magasins | `list_stores`, `create_store`, `update_store`, `delete_store` |
| Produits et prix | `search_products`, `create_product`, `update_product`, `archive_product`, `add_price`, `remove_price` |
| Avis | `list_reviews`, `save_review` (crée ou remplace l'avis d'un profil), `delete_review` |
| Tickets | `list_receipts`, `get_receipt_photo` (Claude lit la photo), `update_receipt` (magasin, date, traité) |

Images par URL : `https` uniquement, adresse publique (jamais le réseau local), JPEG,
PNG ou WebP de 10 Mo au plus, téléchargées en 20 secondes au plus.

Tickets : la photo se prend toujours dans l'application. Ensuite, par exemple : « Traite
mon dernier ticket ». Claude lit la photo, propose un produit pour chaque ligne, saisit
les prix que tu confirmes et marque le ticket traité.

## Dépannage

| Symptôme | Cause probable |
| --- | --- |
| « Couldn't reach the MCP server » | Adresse sans `/mcp`, ou API injoignable depuis Internet |
| Erreur `invalid_client` après le clic sur Se connecter | Client Claude inconnu : son document de métadonnées n'a pas pu être lu (journaux de l'API, « Client metadata document ») |
| Retour en boucle sur l'écran de connexion | Adresse Google non autorisée (`AUTH_ALLOWED_EMAIL`) |
| Les nouveaux outils n'apparaissent pas | claude.ai garde l'ancienne liste : déconnecter puis reconnecter le connecteur, ou ouvrir une nouvelle discussion |
