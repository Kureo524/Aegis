# Icon baking

`python spec.py` (choix des glyphes, écrit spec.json) puis `python make_icons.py spec.json $GAME_ICONS`
(dépôt https://github.com/game-icons/icons dézippé ; `rsvg-convert` et Pillow requis). Licence des glyphes :
CC BY 3.0, crédits dans `Assets/GraphicAssets/Sprites/ICONS.md`.

Les enclumes légendaires par rôle (`Items/Anvil_Legendary_<Rôle>.png`) : `python role_anvils.py` après
`make_icons.py` (enclume légendaire + glyphe du rôle `UI/Roles` dans un médaillon).
