# Sprite baking (modèles 3D CC0 → planches 2.5D)

Produit `Assets/GraphicAssets/Sprites/Characters/Sheets/*` et le sol de l'arène.
Dossier de travail : `$AEGIS_BAKE` (défaut `/tmp/aegis-bake`) ; les modèles glTF (Quaternius, CC0)
vont dans `$AEGIS_BAKE/models/` (chemins dans `chars.json`).

1. **Rendu** : `blender -b --factory-startup -P render_sprites.py` (Blender 5.1) ; `ONLY=Vesna,Wolf`
   pour n'en refaire que certains. Clips, recoloration, braise, pierre : `chars.json`.
2. **Planches** : `python pack.py` (découpe serrée par image, contour, pivot aux pieds), puis
   `python calibrate.py` (pixels par unité : tailles relatives au héros).
3. **Portraits** HUD : `blender -b --factory-startup -P portraits.py`.
4. Copier `sprites/<Nom>.png` et `sprites/<Nom>.json` (renommé `<Nom>.sheet.json`) dans
   `Assets/GraphicAssets/Sprites/Characters/Sheets/`, puis `EditorTools.ProjectSetup.Run`.

Sol de l'arène : `python paint_ground.py Assets/GraphicAssets/Environment/Ground`.
Icônes : voir `Tools/IconBaking` (game-icons.net, CC BY 3.0, crédits dans `Sprites/ICONS.md`).
