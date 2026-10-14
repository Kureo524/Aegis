# Sprites des personnages

Planches pré-rendues dans Blender 5.1 (EEVEE, caméra orthographique à l'inclinaison du jeu ~51°,
vue 3/4 tournée vers la droite, contour sombre, pivot aux pieds) par `Tools/SpriteBaking`.

| Planche | Modèle source | Licence |
|---|---|---|
| Vesna | « Adventurer » de Quaternius (https://quaternius.com), recoloré, braise ajoutée | CC0 |
| Goblin | « Orc » (Ultimate Monsters), Quaternius | CC0 |
| SkeletonArcher | « Skeleton », Quaternius | CC0 |
| Brute | « Blue Demon » (Ultimate Monsters), Quaternius | CC0 |
| Wolf | « Wolf » (Animated Animals), Quaternius | CC0 |
| FrostSpecter | « Ghost » (Ultimate Monsters), Quaternius | CC0 |
| MaskedShaman | « Tribal » (Ultimate Monsters), Quaternius | CC0 |
| StoneGolem | « Yeti » (Ultimate Monsters), Quaternius, en pierre | CC0 |
| Grokk | « Demon » (Ultimate Monsters), Quaternius | CC0 |
| TrainingDummy | modélisé dans `render_sprites.py` | — |

Les modèles ont été téléchargés depuis poly.pizza (qui héberge le catalogue Quaternius en CC0).
Chaque `*.sheet.json` donne le rectangle et le pivot de chaque image et les clips (idle, run,
attack, hit, death, cast…). `EditorTools.CharacterSpritesSetup` découpe les planches et crée les
`SpriteAnimationSet` (Assets/Data/Characters/Animations).
