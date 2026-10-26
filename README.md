# Aegis — La Horde

Projet étudiant : un **jeu de survie contre des hordes** en 2.5D, aux commandes façon MOBA, réalisé sous
**Unity 6.3** en C#, avec une architecture soignée. Choisissez votre champion, tenez une clairière assiégée
vague après vague, montez de niveau, construisez votre build avec des objets et des augments, puis abattez
Grokk, le démon de la horde.

## Télécharger et jouer

Les builds sont sur la page des [**releases**](https://github.com/Kureo524/Aegis/releases/latest) :

| Système | Archive | Lancement |
|---|---|---|
| **Windows** 10/11 (64 bits) | `Aegis-<version>-Windows.zip` | extraire, puis lancer `Aegis.exe` |
| **Linux** (x86_64) | `Aegis-<version>-Linux.tar.gz` | `tar xzf Aegis-*-Linux.tar.gz && ./Aegis/Aegis.x86_64` |

Le jeu n'est pas signé : Windows SmartScreen peut afficher « Windows a protégé votre ordinateur », il
suffit de cliquer sur *Informations complémentaires* puis *Exécuter quand même*.

## Run courte (rendu B3 Workshop)

Le mode par défaut, sur l'écran de sélection du champion, est la **Run courte** : 2 vagues, un choix
d'augment, un **score final** et la sauvegarde des éclats de méta-progression, en 2 à 3 minutes. Le
bouton **Mode** lance la partie complète.

Le document de rendu (choix du moteur, schémas d'architecture, choix techniques et limites) :
[**`Docs/Rendu.pdf`**](Docs/Rendu.pdf) (version Markdown : [`Docs/Rendu.md`](Docs/Rendu.md)).

## Le jeu

- **Deux champions**, choisis à l'écran de sélection :
  - **Vesna, la Lanceuse de braises** (distance, magie de feu) : son passif *Surchauffe* charge l'attaque
    qui suit chaque sort ; *Gerbe de braises* en cône, *Bombe de cendres*, *Roulade*, et l'ultime
    *Pluie de braises* qui écrase une zone entière.
  - **Aldric, le Gardien de l'aurore** (mêlée, givre) : *Givre mordant* toutes les trois attaques,
    *Taille boréale*, *Rempart de givre*, *Bond polaire*, et l'ultime *Couronne d'aurore*.
- **Boucle rogue-lite** : vagues à quota et minuterie, une minute de pause entre les vagues, boss, vagues
  bonus ; boutique accessible à tout moment, enclumes, élixirs ; 68 objets à passif propre, dont certains
  grandissent au fil de la partie ; 43 augments en draft (une relance par carte, relance dorée).
- **Bestiaire** : gobelins, archers squelettes, brutes, loups, spectres glacials, chamans masqués, golems de
  pierre, des **élites** qui portent des objets, et Grokk.
- **Méta-progression** persistante : éclats gagnés en run, un arbre de talents par champion.

## Commandes

Les touches de sort sont liées **par position**, comme dans un MOBA : A Z E R sur un clavier AZERTY,
Q W E R sur un QWERTY. La disposition du clavier se règle dans les options (AZERTY par défaut).

| Touche (AZERTY) | Action |
|---|---|
| **Clic droit** | Se déplacer ; sur un ennemi, l'attaquer |
| **A Z E R** | Sorts du champion (maintenir pour viser, relâcher pour lancer) |
| **Ctrl + A Z E R** | Monter le rang d'un sort (ou le bouton « + » du HUD) |
| **D F** | Sorts d'invocateur |
| **Q** maintenu + **clic gauche** | Attaque-déplacement |
| **S** | Stop |
| **P** | Boutique (achats entre les vagues) |
| **Espace** / clic molette | Caméra verrouillée / libre |
| **Molette** | Zoom |
| **Échap** | Annuler une visée, sinon pause |

## Architecture

Quatre règles tenues sur tout le code (résumé et schémas dans [`Docs/Rendu.md`](Docs/Rendu.md), détail dans
[`Docs/CharacterSystem_Design.md`](Docs/CharacterSystem_Design.md), la spécification de travail) :

1. **State machines** pour les états du jeu et de la run.
2. **Data-driven** : personnages, sorts, objets, augments, vagues, arènes… sont des ScriptableObjects,
   générés par des scripts d'éditeur (`Assets/Editor`, `EditorTools.ProjectSetup.Run`).
3. **Injection de dépendances** avec [VContainer](https://github.com/hadashiA/VContainer) : aucun singleton.
4. **EventBus** : aucune référence directe entre systèmes.

UI entièrement en **UI Toolkit**. Plus de 200 tests (EditMode et PlayMode, dont des tests d'architecture
qui vérifient l'absence d'état statique et les dépendances entre couches), plus un bot d'équilibrage qui
joue la partie entière (`-testFilter BalanceSimulation`).

## Lancer le projet

1. Ouvrir le dossier avec **Unity 6000.3.x** (URP, Input System, AI Navigation, VContainer : résolus par le
   Package Manager).
2. Ouvrir `Assets/Scenes/Releases/Menu.unity` et lancer le jeu ; `Assets/Scenes/Tests/TestScene.unity`
   démarre directement une partie.
3. Les contenus se régénèrent avec le menu **Tools** ou en ligne de commande :
   `Unity -batchmode -quit -executeMethod EditorTools.ProjectSetup.Run`.

### Builds

Menu **Tools > Build Linux (Game)** / **Tools > Build Windows (Game)**, ou en ligne de commande :

```sh
Unity -batchmode -quit -projectPath . -executeMethod EditorTools.BuildScript.BuildLinuxGame    # Builds/Linux
Unity -batchmode -quit -projectPath . -executeMethod EditorTools.BuildScript.BuildWindowsGame  # Builds/Windows
```

La build Windows demande le module *Windows Build Support (Mono)* de l'éditeur.

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
