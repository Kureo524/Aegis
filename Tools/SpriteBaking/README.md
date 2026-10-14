# Sprite baking (personnages LoL → planches 2.5D)

Chaîne qui produit `Assets/GraphicAssets/Sprites/Characters/LoL/*` et le sol de l'arène.
Dossier de travail : `$AEGIS_BAKE` (défaut `/tmp/aegis-bake`).

1. **Modèles** : `python export.py <id> <alias> $AEGIS_BAKE/lol/glb/<alias>.glb [extras]`
   (Playwright + Chromium : ouvre https://modelviewer.lol et appelle son propre export GLB, toutes
   animations comprises). Ex. `export.py 81000 ezreal …` ; monstres : `export.py 0 sru_krug … extras`.
2. **Rendu** : `blender -b --factory-startup -P render_sprites.py` (Blender 5.1, EEVEE, caméra
   orthographique à l'inclinaison du jeu ~51°, vue 3/4 vers la droite) ; `ONLY=Ezreal,Krug` pour
   n'en refaire que certains. Clips et nombre d'images : `chars.json`.
3. **Planches** : `python pack.py` (découpe serrée par image, contour sombre, pivot aux pieds), puis
   `python calibrate.py` (pixels par unité : tailles relatives à Ezreal).
4. **Portraits** HUD : `blender -b --factory-startup -P portraits.py`.
5. Copier `sprites/<Nom>.png` et `sprites/<Nom>.json` (renommé `<Nom>.sheet.json`) dans
   `Assets/GraphicAssets/Sprites/Characters/LoL/`, puis `EditorTools.ProjectSetup.Run`
   (`CharacterSpritesSetup` découpe les planches et crée les `SpriteAnimationSet`).

Sol de l'arène : `python paint_ground.py Assets/GraphicAssets/Environment/Ground`.

Les modèles appartiennent à Riot Games (projet de fan privé, non commercial) : voir le README des
planches.
