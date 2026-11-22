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
| Aliments | `search_foods` (filtres famille, rôle, conservation, étiquette, mois de saison), `get_food` (avec produits et avis), `create_food` (famille obligatoire), `update_food` (remplace toute la fiche, classement compris), `archive_food`, `set_food_image_from_url`, `remove_food_image` |
| Recettes | `search_recipes`, `get_recipe` (ingrédients, nutrition par portion, coût min–max, avis), `create_recipe` (avec ses ingrédients), `update_recipe`, `add_recipe_ingredient`, `update_recipe_ingredient`, `remove_recipe_ingredient`, `archive_recipe`, `set_recipe_image_from_url`, `remove_recipe_image` |
| Magasins | `list_stores`, `create_store`, `update_store`, `delete_store` |
| Produits et prix | `search_products`, `create_product`, `update_product`, `archive_product`, `add_price`, `remove_price` |
| Avis | `list_reviews`, `save_review` (crée ou remplace l'avis d'un profil), `delete_review` |
| Tickets | `list_receipts`, `get_receipt_photo` (Claude lit la photo), `update_receipt` (magasin, date, traité) |
| Sport | `get_sport_catalog` (sports et date du jour), `list_sport_templates`, `create_sport_template`, `update_sport_template`, `archive_sport_template`, `list_sport_sessions`, `log_sport_session`, `update_sport_session`, `delete_sport_session`, `get_sport_weeks` (bilan et objectifs) |
| Objectifs et défis | `list_weekly_goals`, `create_weekly_goal`, `change_weekly_goal`, `stop_weekly_goal`, `list_daily_challenges` (série, jokers, paliers), `create_daily_challenge`, `update_daily_challenge`, `stop_daily_challenge`, `add_challenge_entry` (« j'ai fait 30 pompes »), `remove_challenge_entry` |
| Routines et exercices | `list_exercises`, `create_exercise` (avec charge habituelle et image par URL), `update_exercise`, `archive_exercise`, `set_exercise_image_from_url`, `remove_exercise_image`, `get_exercise_progress` (records, meilleure valeur et charge par semaine, dernières coches avec effort et gêne), `list_routines` (exercices, cibles, passages du jour, série, jokers), `create_routine`, `update_routine`, `stop_routine`, `complete_exercise` (« j'ai fait la chaise, 4 séries de 45 s », dans le passage en cours ou un nouveau, avec effort et gêne), `uncomplete_exercise`, `log_routine_session` (un passage entier en un appel), `delete_routine_session` |
| Todos | `get_tasks_today` (en retard, aujourd'hui, 7 jours, sans échéance, routines du foyer), `search_todos` (texte, état, étiquette, projet), `get_todo`, `get_task_tags`, `create_todo` (avec sous-tâches, projet et image par URL), `update_todo` (reporter = nouvelle échéance), `complete_todo`, `reopen_todo`, `abandon_todo`, `add_checklist_item`, `update_checklist_item`, `remove_checklist_item`, `set_todo_image_from_url` |
| Routines du foyer | `list_task_routines` (prochaine échéance, retard, série), `get_task_routine` (passages), `create_task_routine` (date fixe ou délai), `update_task_routine`, `complete_task_routine` (« j'ai changé les draps hier »), `skip_task_routine`, `remove_task_routine_pass`, `pause_task_routine` (et reprise), `stop_task_routine`, `set_task_routine_image_from_url` |
| Projets | `list_projects` (avancement), `get_project` (ses todos), `create_project`, `update_project`, `close_project` (terminer, abandonner, rouvrir), `set_project_image_from_url` |
| Santé : compléments | `list_supplements` (coût par mois, coche du jour, série), `create_supplement` (dose, moment, conditionnement, liens d'achat, cure), `update_supplement`, `set_supplement_taken` (« j'ai pris ma vitamine D »), `stop_supplement` (et reprise) |
| Santé : symptômes | `get_symptom_journal` (journal et liste suivie), `create_symptom_type`, `log_symptom` (« j'ai mal au ventre, 6/10 »), `delete_symptom_entry`, `save_symptom_checkin` (bilan du jour, « rien à signaler ») |
| Santé : analyses et mesures | `list_markers` (position, tendance, à refaire), `create_marker`, `update_marker`, `list_analyses`, `create_analysis` (valeurs lues d'un document, après confirmation), `update_analysis`, `delete_analysis`, `list_body_measures`, `create_body_measure_type`, `add_body_measure`, `delete_body_measure` |
| Santé : documents et rendez-vous | `list_health_documents`, `get_health_document_file` (Claude lit la photo ou le PDF), `create_health_document`, `update_health_document`, `list_appointments`, `create_appointment` (depuis un e-mail de confirmation ou l'agenda), `update_appointment` (rendez-vous déplacé, compte rendu), `delete_appointment` |

Images par URL : `https` uniquement, adresse publique (jamais le réseau local), JPEG,
PNG ou WebP de 10 Mo au plus, téléchargées en 20 secondes au plus.

Santé : Claude ne voit que la santé du compte connecté. Un document (ordonnance, résultat…)
se photographie ou s'envoie en PDF dans l'application ; ensuite, par exemple : « Lis mon
dernier bilan sanguin et saisis les valeurs ». Claude lit le fichier, propose les marqueurs
et valeurs, saisit l'analyse reliée au document après ta confirmation. Rendez-vous, avec les
connecteurs Gmail ou Google Agenda de Claude : « Cherche mes confirmations Doctolib des 30
derniers jours et mets à jour mes rendez-vous LifeOS (sans doublon, montre-moi d'abord) ».
LifeOS ne se connecte jamais à Doctolib.

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
