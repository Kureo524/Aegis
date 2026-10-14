# Aegis — La Horde

Projet étudiant : un **jeu de survie contre des hordes** en 2.5D, aux commandes façon MOBA, réalisé sous
**Unity 6.3** en C#, avec une architecture soignée. Vous incarnez **Vesna, la Lanceuse de braises**, dans
une clairière assiégée : tenez les vagues, montez de niveau, construisez votre build avec des objets et des
augments, puis abattez Grokk, le démon de la horde.

## Le jeu

- **Commandes façon MOBA** : clic droit pour se déplacer ou attaquer, sorts sur les quatre touches de gauche
  (A Z E R sur un clavier AZERTY, Q W E R sur un QWERTY : les touches sont liées par position), sorts
  d'invocateur sur D et F, attaque-déplacement, caméra verrouillée ou libre (Espace).
- **Vesna** : son passif *Surchauffe* charge l'attaque qui suit chaque sort. Elle lance une *Gerbe de
  braises* en cône et une *Bombe de cendres*, esquive d'une *Roulade*, et son ultime, la *Pluie de braises*,
  écrase une zone entière.
- **Boucle rogue-lite** : vagues à quota et minuterie, boss, vagues bonus ; boutique, enclumes, élixirs ;
  65 objets à passif propre ; 42 augments en draft (une relance par carte, relance dorée) ; méta-progression
  persistante (éclats, arbre de talents du champion).
- **Bestiaire** : gobelins, archers squelettes, brutes, loups, spectres glacials, chamans masqués, golems de
  pierre, et Grokk.

## Architecture

Quatre règles tenues sur tout le code (voir `Docs/CharacterSystem_Design.md`, la source de vérité) :

1. **State machines** pour les états du jeu et de la run.
2. **Data-driven** : personnages, sorts, objets, augments, vagues, arènes… sont des ScriptableObjects,
   générés par des scripts d'éditeur (`Assets/Editor`, `EditorTools.ProjectSetup.Run`).
3. **Injection de dépendances** avec [VContainer](https://github.com/hadashiA/VContainer) : aucun singleton.
4. **EventBus** : aucune référence directe entre systèmes.

UI entièrement en **UI Toolkit**. Près de 200 tests (EditMode et PlayMode), plus un bot d'équilibrage
qui joue la partie entière (`-testFilter BalanceSimulation`).

## Lancer le projet

1. Ouvrir le dossier avec **Unity 6000.3.x** (URP, Input System, AI Navigation, VContainer : résolus par le
   Package Manager).
2. Ouvrir `Assets/Scenes/Releases/Menu.unity` et lancer le jeu ; `Assets/Scenes/Tests/TestScene.unity`
   démarre directement une partie.
3. Les contenus se régénèrent avec le menu **Tools** ou en ligne de commande :
   `Unity -batchmode -quit -executeMethod EditorTools.ProjectSetup.Run`.

`Tools/SpriteBaking` (Blender 5.1) produit les sprites animés des personnages à partir de modèles 3D ;
`Tools/IconBaking` génère les icônes.

## Crédits

- **Modèles 3D des personnages** : [Quaternius](https://quaternius.com) (CC0), rendus en sprites par
  `Tools/SpriteBaking` ; le mannequin d'entraînement est modélisé par le script.
- **Décor de l'arène** : [Nature Kit de Kenney](https://kenney.nl) (CC0), recoloré.
- **Icônes** : [game-icons.net](https://game-icons.net), sous licence
  [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). Auteurs détaillés dans
  `Assets/GraphicAssets/Sprites/ICONS.md`.
- **Polices** : Noto Sans et Noto Serif (SIL Open Font License, `Assets/UI/Fonts`).
- Sol de l'arène, interface et textures : générés pour le projet.

Les mécaniques s'inspirent des MOBA et de leurs modes arène ; tout le contenu (champion, sorts, objets,
augments, créatures, noms, visuels) est original ou sous licence libre.
