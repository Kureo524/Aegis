# Système de personnage modulaire — Design Doc (Unity 6.4, C#)

> Objectif : un **seul système** réutilisable pour le joueur, les ennemis, les alliés, les boss… Un personnage = un **prefab générique** + une **définition de données** (ScriptableObject). On ne code pas un perso, on le *configure*.

> **Contraintes d'architecture OBLIGATOIRES** (non négociables, détail et conventions au §14) :
> 1. **State machine obligatoire** pour les états du jeu (`Menu`, `Loading`, `Game`, `Pause`, `Win`, `GameOver`… selon les besoins). Aucun `bool isPaused`, tout changement passe par `TryChange`.
> 2. **Architecture data-driven** : tout contenu et tout réglage est un **ScriptableObject** ; le code ne contient aucune valeur d'équilibrage.
> 3. **Injection de dépendances avec VContainer** : **aucun singleton**, aucun `static` mutable, aucun `FindObjectOfType` / `Camera.main` dans le code de jeu.
> 4. **EventBus** (`IEventBus`, injecté) pour communiquer entre systèmes : **aucune référence dure entre systèmes**.
>
> Tout code qui viole une de ces règles est un bug à corriger, même s'il « marche ».

---

## 1. Principes d'architecture

1. **Composition > héritage** : pas de `class Player : Character`. Des petits composants indépendants sur un même GameObject.
2. **Data-driven** : tout ce qui diffère d'un perso à l'autre vit dans des ScriptableObjects (stats de base, sprite, sorts, passif…).
3. **Séparation Intention / Exécution** : le mouvement ne sait pas *qui* le commande (joueur, IA, replay). Il reçoit une direction.
4. **Les stats ne sont jamais modifiées directement** : on ajoute/retire des *modificateurs* (traçables, annulables, temporaires).
5. **Communication par events** : `IEventBus` entre systèmes (§14.3), `event` C# uniquement à l'intérieur d'un même personnage (`CharacterEvents`).
6. **Pooling** dès le départ pour tout ce qui spawn en masse (ennemis, projectiles, textes de dégâts).
7. **Les exemples sont illustratifs** : un augment, un passif ou un sort cité dans ce doc n'est qu'un exemple parmi d'autres. Chaque système (passifs, augments, items, enclumes, sorts) doit être **générique et piloté par les données**, jamais limité aux exemples.
8. **Contraintes techniques obligatoires** : state machines pour les états de jeu, EventBus pour la communication entre systèmes, VContainer pour l'injection, ScriptableObjects pour toutes les données, projet rangé et versionné avec Git (détail §14).

---

## 2. Vue d'ensemble

```
Character (prefab)
├── CharacterRoot            (GameObject, Rigidbody 3D + BoxCollider)
│   ├── Character            → point d'entrée, référence les autres composants, init depuis CharacterDefinition
│   ├── StatController       → possède toutes les stats + modificateurs
│   ├── Health               → PV, bouclier, mort, invincibilité
│   ├── CommandController    → ordre courant (Move / AttackTarget / Cast / Stop)
│   ├── ICommandSource       → PlayerCommandInput (souris + clavier) | AI | ...
│   ├── CharacterMovement    → déplace le Rigidbody vers un point
│   ├── BasicAttacker        → portée, windup, cadence, auto-attaque d'une cible
│   ├── ManaPool             → mana courant / max / regen
│   ├── AbilityCaster        → sorts du champion Q W E R (rangs, mana, délais) et d'invocateur D F (§7)
│   ├── PassiveController    → passif du perso, + passifs d'items et d'augments (CharacterEvents)
│   ├── CapabilityController → capacités (ex. SpellCrit)
│   ├── Wallet / Inventory   → gold et items (joueur)
│   ├── AugmentController    → augments choisis pendant la run
│   ├── CharacterLevel       → XP, niveau, points de compétence, croissance des stats
│   ├── DamageReceiver       → reçoit un DamageInfo, applique la formule
│   └── Visual (enfant)
│        ├── SpriteRenderer
│        ├── Animator (optionnel)
│        └── BillboardSprite → garde le sprite face caméra
```

### Flux de données

```
CharacterDefinition (SO)  ──►  Character.Init()  ──►  StatController (stats de base)
                                                  ├─►  ManaPool (MaxMana, ManaRegen)
                                                  └─►  AbilityCaster (sorts Q W E R + D F, rangs)
ICommandSource ──► CommandController ──┬─► CharacterMovement ──► Rigidbody
                                       ├─► BasicAttacker ──► DamageInfo ──► DamageReceiver (cible) ──► Health
                                       └─► AbilityCaster ──► SpellCaster ──► SpellExecutor(CastContext, rang) ──► DamageInfo ──► ...
```

---

## 3. Personnage 2.5D

### Plan de jeu : **XZ** (tranché)

Déplacement sur le plan **XZ** (vue de dessus), caméra inclinée, sprites en billboard. Tous les calculs de pointage et de portée se font sur ce plan (Y = hauteur, ignorée).

### Setup du prefab
- **Rigidbody 3D** : `Use Gravity` selon le besoin (désactivé si sol plat sans relief), `Freeze Rotation` X/Y/Z, `Interpolation = Interpolate`, `Collision Detection = Continuous Dynamic` pour les rapides.
- **BoxCollider** : taille adaptée aux pieds/corps, pas à toute la hauteur du sprite.
- **Sprite** : `SpriteRenderer` sur un **enfant** (jamais sur le root qui porte la physique).
- **BillboardSprite** : le sprite regarde la caméra (ou rotation X fixe si caméra à angle constant, ce qui évite le jitter et coûte moins cher).
- **Flip** : `spriteRenderer.flipX` selon la direction horizontale, pas de `scale.x = -1`.
- **Tri d'affichage** : trier par profondeur (Z) via *Transparency Sort Axis* (Graphics Settings) ou `sortingOrder` dérivé de la position Z.
- **Layers physiques** : `Player`, `Enemy`, `PlayerProjectile`, `EnemyProjectile`, `Pickup`. Matrice de collision configurée dès le début.

### Prefabs (en place)

**Un prefab générique, des variants pour qui le pilote, des données pour le reste** :

| Prefab (`Assets/Prefabs/Characters/`) | Contenu |
|---|---|
| `Character` (base) | Racine physique, **pivot aux pieds** : `Rigidbody` (interpolé, rotation X/Z gelée), `BoxCollider`, `NavMeshAgent`, `CharacterMovement`, `Character`. Enfant **`Visual`** : `SpriteRenderer` + `CharacterVisual` |
| `Player` (variant de `Character`) | + `PlayerCommandInput` |
| `TrainingDummy` (variant de `Character`) | + `RespawnOnDeath` (réapparaît 2 s après sa mort) |
| `Enemy` (variant de `Character`) | + `EnemyAI` ; collisions discrètes (des centaines de corps) |

Le prefab de base porte aussi `BasicAttacker` et une **barre de PV** (`HealthBarView` : deux sprites face caméra au-dessus de la tête, verte pour le joueur, rouge pour les ennemis ; en sprites plutôt qu'en UI pour rester léger avec des centaines d'entités).

- Ce qui distingue deux persos d'un même type (**sprite**, **taille du corps** `BodySize`, stats…) vient de la `CharacterDefinition`, appliquée par `Character.Init` : jamais un variant par personnage.
- **`CharacterVisual`** : billboard (copie la rotation de la caméra, donc le sprite est vu à plat, sans déformation de perspective) et `flipX` selon le déplacement horizontal **à l'écran**. Les sprites sont dessinés **tournés vers la droite**, pivot en bas au centre. *Pour les centaines d'ennemis : un `LateUpdate` par entité, à centraliser si le profiler le demande (200 ennemis tiennent aujourd'hui en ~1,6 ms par frame dans l'éditeur).*
- Le joueur n'est **pas posé dans la scène** : `PlayerSpawner` (entry point du `GameLifetimeScope`) l'instancie via `IObjectResolver.Instantiate` au `PlayerSpawn`, appelle `Init` et publie `PlayerSpawnedEvent`, que la caméra écoute pour le suivre.
- Sprites provisoires (64 × 128, 64 px/unité, `Assets/GraphicAssets/Sprites/Characters/`) : `Placeholder_Hero` (généré par *Project Setup*, aperçu du prefab de base) et **`Chibi_Explorer`**, utilisé par `Char_TestHero` : chibi pixel-art *inspiré* d'Ezreal (League of Legends). Placeholder de prototype uniquement : à remplacer par une art originale avant toute diffusion publique (propriété intellectuelle de Riot Games). Ennemis (originaux, provisoires) : `Enemy_Goblin`, `Enemy_SkeletonArcher` (64 × 128) et `Enemy_Brute` (96 × 160), et le boss `Boss_GoblinKing` (128 × 192).

---

## 4. Ordres, déplacement et attaque de base (façon LoL)

### 4.1 Contrôles

| Input | Effet |
|---|---|
| **Clic droit sur le sol** | `MoveCommand(point)` |
| **Clic droit sur un ennemi** | `AttackTargetCommand(ennemi)` : le perso se rapproche jusqu'à sa portée puis attaque en boucle |
| **Maintenir Q** (AZERTY ; A en QWERTY) + **clic gauche** | *Attack-move* (§4.6) : affiche la portée d'attaque tant que la touche est maintenue ; le clic lance la marche vers le point en attaquant ce qui croise sa route |
| **A / Z / E / R** (AZERTY ; Q W E R en QWERTY) | Sorts du champion (§7). Par défaut (**rapide avec indicateur**) : maintenir = portée et forme affichées, relâcher = lancer vers le curseur. Selon l'option : visée puis clic gauche (Normal), ou lancer immédiat (Rapide) |
| **Ctrl + A / Z / E / R** | Dépense un point de compétence sur le sort (§7.7) ; aussi via le bouton « + » du HUD |
| **D / F** | Sorts d'invocateur (F = Flash ; D = sort ramassé, §9.5) |
| **S** | `StopCommand` (annule l'ordre en cours) |
| **Échap / clic droit** pendant une visée | Annule la visée |
| **Échap** sans visée | Pause (menu : Reprendre / Options / Menu principal / Quitter) ; dans la pause, Échap revient en arrière puis reprend |
| **Espace** (ou clic molette) | Caméra **verrouillée / libre** (toggle, §4.8) |
| **Molette** | Zoom caméra |
| **Curseur au bord de l'écran** | Déplace la caméra quand elle est **libre** (edge scrolling façon LoL) |

### 4.2 Système d'ordres (Command pattern)

Le joueur et l'IA ne manipulent plus le mouvement directement : ils **émettent des ordres**, un seul est actif à la fois.

```csharp
public abstract class CharacterCommand { }
public sealed class MoveCommand : CharacterCommand { public Vector3 Point; }
public sealed class AttackTargetCommand : CharacterCommand { public Character Target; }
public sealed class AttackMoveCommand : CharacterCommand { public Vector3 Point; public Character PreferredTarget; }
public sealed class CastCommand : CharacterCommand { public int Slot; public CastContext Context; }
public sealed class StopCommand : CharacterCommand { }

public interface ICommandSource { void Tick(CommandController controller); }
```

- `CommandController` garde l'ordre courant et le remplace quand un nouveau arrive (sauf si l'action en cours n'est pas annulable : cast en windup).
- `PlayerCommandInput : ICommandSource` traduit souris/clavier en ordres. Les ennemis utiliseront une IA qui émet les mêmes ordres (ex. `AttackTargetCommand(joueur)`), donc **même code pour tout le monde**.

### 4.3 Sélection de la cible et du point au sol

- **Point au sol** : `Plane.Raycast` sur le plan XZ (voir §7.5).
- **Ennemi sous le curseur** (clic droit) : raycast sur le layer `Enemy`. Les colliders de sprites fins sont durs à toucher, donc ajouter un **rayon de tolérance** : si le raycast ne touche rien, prendre l'ennemi le plus proche du curseur dans un rayon `PickRadius` (configurable). Sinon → c'est un ordre de déplacement.
- **Feedback** : marqueur au sol (vert = déplacement, rouge = attaque), surbrillance de l'ennemi visé.

### 4.4 Déplacement vers un point

- `CharacterMovement.SetDestination(Vector3 point, float stopDistance)`.
- `FixedUpdate` : `rb.linearVelocity = direction * MoveSpeed` (**`linearVelocity`** en Unity 6, `velocity` est déprécié) ; arrêt quand la distance < `stopDistance` ; la vitesse vient de la stat `MoveSpeed` → **même code pour tous, vitesse différente**.
- Flip du sprite selon la direction horizontale.
- **Contournement des obstacles (NavMesh, requis)** : package **AI Navigation** ; `NavMeshSurface` sur le sol (plan XZ), obstacles statiques bakés, `NavMeshObstacle` (carving) pour les obstacles dynamiques.
  - Le clic droit est ramené sur une zone marchable avec `NavMesh.SamplePosition` (si le clic tombe hors navmesh, on prend le point valide le plus proche).
  - Le `NavMeshAgent` ne sert qu'à **calculer le chemin** (`updatePosition = false`, `updateRotation = false`). Le Rigidbody est piloté par `agent.desiredVelocity.normalized × MoveSpeed`, puis `agent.nextPosition = rb.position` à chaque `FixedUpdate` pour garder les deux synchronisés. `agent.speed = MoveSpeed`, `agent.stoppingDistance = stopDistance`.
  - Poursuite d'une cible mobile : `SetDestination(target.position)` **limité à quelques fois par seconde**.
  - Knockback / dash : agent désactivé le temps de l'impulsion, puis `Warp` sur la position finale.
- Prévoir : `CanMove` (stun/root), `Knockback` (impulsion qui court-circuite l'ordre en cours).

### 4.5 Attaque de base (`BasicAttacker`)

Machine à états : `Idle → Chasing → Windup → Cooldown → (re-attaque si la cible est toujours valide)`.

- **Poursuite** : tant que `distance(cible) > AttackRange`, mise à jour de la destination vers la position actuelle de la cible. La poursuite s'arrête dès que la cible est à portée.
- **Windup** : délai avant l'impact (`WindupPercent × intervalle`). Un nouvel ordre de déplacement **annule** l'attaque pendant le windup (comme LoL : l'attaque ne part pas, mais **son timer n'est pas consommé** : on peut relancer une attaque aussitôt). Après l'impact, on peut bouger sans perdre la cadence. Test Hero : `WindupPercent` = **0,18** (≈ 0,28 s à 0,65 AS, proche d'un tireur de LoL). **Feedback visuel** (`CharacterVisual`, sans Animator) : pendant le windup le sprite se penche en arrière et se tasse, à l'impact il se fend vers la cible ; sans lui, le windup se lisait comme une pause.
- **Cadence** : `intervalle = 1 / AttackSpeed` (stat finale).
- **Impact** : crée un `DamageInfo` avec `IsBasicAttack = true` (→ déclenche le `Lifesteal`, voir §6), scaling sur `PhysicalAttack`.
- **Mêlée vs distance** : `BasicAttackDefinition` (ScriptableObject) avec `WindupPercent`, `ProjectilePrefab` (null = coup instantané), `DamageRatio`, `DamageType`. Chaque `CharacterDefinition` référence la sienne. La **portée** n'y figure pas : c'est la stat `AttackRange` (une seule source de vérité, elle évolue avec les niveaux et modificateurs).
- **Cible invalide** (morte, hors jeu) : avec un clic droit (`AttackTargetCommand`), le perso s'arrête. Pas d'attaque automatique en idle côté joueur. En attack-move, voir §4.6.

> **En place** : `BasicAttacker` (sur le prefab `Character`) implémente `Idle → Chasing → Windup → impact`, la portée centre à centre moins le rayon de la cible, la poursuite (chemin rafraîchi toutes les 0,2 s), la cadence `1 / AttackSpeed`, le windup perdu si un ordre de déplacement arrive, le crit (`CritChance` / `CritDamage`) et le sprite tourné vers la cible. `PlayerCommandInput` : clic droit sur un ennemi (raycast, sinon le plus proche dans `pickRadius` autour du point cliqué) → attaque, marqueur rouge ; clic droit au sol → déplacement, attaque annulée. **Projectiles** : si `BasicAttackDefinition.ProjectilePrefab` est renseigné, l'impact tire un `Projectile` **à tête chercheuse** (une auto-attaque LoL ne rate pas) vers le torse de la cible, à `ProjectileSpeed`, avec les dégâts figés au tir ; il disparaît si la cible meurt en vol. Instances **poolées** (`ProjectileLauncher`, service du `GameLifetimeScope`, `ObjectPool` par prefab, création via le conteneur). Test Hero : `Projectile_ArcaneBolt` (orbe face caméra + traînée). *Reste : `CommandController` / commandes (§4.2).*

### 4.6 Q + clic gauche : attack-move (comportement LoL)

**Saisie**
1. **Maintenir Q** → état `Targeting` : cercle d'indicateur de **portée d'attaque** (`AttackRange`) autour du perso, **tant que Q est enfoncé** (relâcher Q annule la visée).
2. **Clic gauche** → `AttackMoveCommand(point, preferredTarget)` :
   - `preferredTarget` = l'ennemi le **plus proche du point cliqué** dans un rayon `AttackMoveSelectRadius`, ou `null` s'il n'y en a pas.
3. **Relâcher Q / clic droit / Échap** : annule la visée sans rien ordonner. Tant que Q reste enfoncé, chaque clic gauche donne un nouvel ordre.

**Exécution (boucle de l'attack-move)**

```
              ┌──────────────────────────────────────────────┐
              ▼                                              │
        [Acquisition] ──cible trouvée──► [Chasing/Windup/Cooldown] ──cible morte/perdue──┘
              │
        aucune cible
              ▼
   [Marche vers le point] ──arrivé──► Idle (fin de l'ordre)
```

1. Si `preferredTarget != null` et valide : c'est la **première cible** (poursuite + attaque, comme un clic droit).
2. Sinon, à chaque frame le perso **scanne** les ennemis dans `AcquisitionRange` (par défaut = `AttackRange`, configurable) :
   - **Aucun** → il continue de marcher vers le point.
   - **Au moins un** → il **s'arrête** et attaque la cible choisie (règle de priorité ci-dessous).
3. Une fois l'attaque lancée (windup), elle va à son terme : on ne change pas de cible en plein windup.
4. **Cible morte ou sortie de portée** → retour à l'étape 2 (re-scan). S'il n'y a plus d'ennemi, la marche vers le point **reprend**.
5. **Arrivée au point** sans ennemi → l'ordre se termine, le perso passe `Idle`.
6. Tout nouvel ordre (clic droit, S, compétence) **remplace** l'attack-move.

**Règle de priorité de cible** (à implémenter via une stratégie `ITargetSelector`) :
- par défaut : l'**ennemi le plus proche du perso** ;
- extensible : priorité par type (boss/élite avant mobs), ou le moins de PV, ou l'ennemi le plus proche du curseur.

**Cas particuliers**
- La portée du scan est évaluée **centre à centre moins le rayon** de la cible (sinon les gros ennemis sont attaqués trop tard).
- Pas d'attaque automatique en dehors d'un attack-move : en `Idle` ou après un clic droit de déplacement, le perso n'attaque **jamais** seul.
- Performance : le scan utilise `Physics.OverlapSphereNonAlloc` sur le layer `Enemy`, à intervalle (ex. toutes les 0,1 s), pas à chaque frame.

> **En place** : `BasicAttacker.AttackMove(point, preferredTarget)` et la visée dans `PlayerCommandInput` (Q affiche le cercle de portée `RangeIndicator`, clic gauche valide, clic droit / Échap annule sans ordre ; `preferredTarget` = ennemi le plus proche du clic dans `attackMoveSelectRadius`, 2 par défaut). Scan toutes les 0,1 s (`OverlapSphereNonAlloc`, sans layers pour l'instant), règle de priorité `ITargetSelector` (`NearestTargetSelector`). Une cible **acquise** qui sort de portée est lâchée et la marche reprend ; une cible **désignée** (preferred) est poursuivie comme un clic droit. L'ordre se termine à l'arrivée. *Reste : un seul mode de visée à la fois avec R et F (phase 5c), layers physiques.*

### 4.7 Mapping clavier AZERTY

Avec le **New Input System**, `<Keyboard>/q` désigne la touche à la **position physique** du Q américain (donc le A sur AZERTY).

**Choix retenu** : les **touches de jeu façon LoL sont liées par position** (groupe de binding `Positional`, jamais remappé) : sorts sur les positions Q W E R (A Z E R en AZERTY), invocateurs D F, attack-move sur la position A (Q en AZERTY), stop sur S. Les autres lettres sont écrites pour la **lettre voulue à sa position QWERTY** et **remappées au démarrage** selon la disposition du joueur : réglage `KeyboardLayout` (`Qwerty` / `Azerty`, **AZERTY par défaut**) dans le SO `ControlsSettings` (`Assets/Data/Settings`), appliqué par `InputReader` via des *binding overrides* (A ↔ Q, Z ↔ W, M → `;`). La disposition sert aussi à afficher les bonnes lettres dans le HUD. Vérifié par `KeyboardLayoutTests` à travers le vrai `InputReader`.

**Rejeté** : les bindings par nom affiché (`<Keyboard>/#(a)`). Le player Linux ne remonte pas la disposition de l'OS (il retombe sur les noms QWERTY : en jeu, « Q » se retrouvait sur la touche A), et l'éditeur Linux segfault en `-batchmode -nographics` en les résolvant.

**Options du joueur** : `UserOptions` (Features/Options, root) garde les choix du joueur en JSON (`persistentDataPath/options.json`, `options.editor.json` dans l'éditeur pour ne jamais toucher aux choix de la build) par-dessus les valeurs par défaut de `ControlsSettings`. Chaque changement est sauvegardé aussitôt et publié (`UserOptionsChangedEvent`) ; les systèmes lisent la valeur au moment où ils en ont besoin. Option en place : **mode de lancement** des sorts (`CastMode` : `Normal`, `QuickWithIndicator`, `Quick` ; défaut `ControlsSettings.CastMode = QuickWithIndicator`). Écran d'options : `OptionsView` (UI Toolkit construit en code), dans le menu pause en jeu et dans `OptionsPanel` (bouton Paramètres du menu principal).

À venir : la disposition clavier dans les options, puis un **menu de rebind** complet.

> **Wrapper C#** : `GameActionMaps.cs` est **généré** par l'Input System depuis le `.inputactions`. Après toute modification des actions : *Tools > Regenerate Input Actions Code*, et committer les deux fichiers ensemble. (L'ancien fichier était un stub écrit à la main que Unity refusait d'écraser ; le `.meta` de l'importer avait aussi des noms de champs invalides, ce qui désactivait la génération.)

---

### 4.8 Caméra (en place)

`CameraController` (Features/CameraControl), réglages dans le SO `CameraSettings` (`Assets/Data/Settings`).

- **Verrouillée** (défaut, `StartLocked`) : suit le joueur avec un offset fixe, lissé (`FollowSpeed`).
- **Libre** : le joueur la déplace en amenant le curseur au **bord de l'écran** (`EdgeSize` px, vitesse proportionnelle à la profondeur dans la zone, lissée par `ScrollSmoothing`), sur le plan XZ, comme dans LoL.
- **Toggle** : `CameraLockEvent` (Espace / clic molette). **Zoom** : `ZoomEvent` (molette), borné par `MinZoom` / `MaxZoom` (5 à 32 depuis le 13/10/2026 : on dézoome bien plus loin).
- **Curseur confiné** à la fenêtre en **build uniquement** (`CursorConfinement`), nécessaire à l'edge scrolling. **Jamais dans l'éditeur** : sous Wayland/Hyprland le verrouillage fuit hors de la Game view et décale le pointeur de tout l'éditeur. Ré-appliqué quand la fenêtre reprend le focus.
---

## 5. Système de stats

### 5.1 Liste des stats (jeu complet façon LoL)

| Catégorie | Stat | Notes |
|---|---|---|
| Vitalité | `MaxHealth` | |
| | `HealthRegen` | PV/s (LoL l'affiche par 5 s ; l'UI peut multiplier par 5) |
| | `MaxMana`, `ManaRegen` | mana/s, même logique |
| Offensif | `PhysicalAttack` (AD), `MagicAttack` (AP) | |
| | `AttackSpeed` | **modèle LoL** : base propre au perso × (1 + bonus %) |
| | `AttackRange` | portée d'attaque de base (affichée par Q) |
| | `CritChance` | 0–1, clampé |
| | `CritDamage` | multiplicateur (ex. 1.75 = +75 %) |
| | `ArmorPenFlat`, `ArmorPenPercent` | **létalité** = `ArmorPenFlat` |
| | `MagicPenFlat`, `MagicPenPercent` | |
| Défensif | `Armor`, `MagicResist` | |
| | `Tenacity` | réduit la durée des **contrôles et des ralentissements** (§5.7) |
| | `SlowResist` (optionnel) | réduit l'intensité des ralentissements |
| Mobilité | `MoveSpeed` | |
| Sustain | `Omnivamp` | % de **tous** les dégâts infligés → soin |
| | `Lifesteal` | % des dégâts des **attaques de base** → soin |
| | `HealShieldPower` | multiplicateur des soins/boucliers **produits** par le perso (y compris sur lui-même) |
| Utilitaire | `AbilityHaste` | réduction effective des cooldowns |
| | `ManaCostReduction` | réduit le coût des sorts (0–0.8, clampé) |

| Converti | `AdaptiveForce` | **force adaptative** (objets prismatiques) : devient **0,6 AD** ou **1 AP** par point, du côté où le perso a le plus de stat **bonus** (AD en cas d'égalité), recalculée à chaque changement (`Character`, modificateur à Source dédiée ; `Character.AdaptiveForceIsMagic` pour l'UI) |

Stats optionnelles style Vampire Survivors (plus tard) : `PickupRange`, `ExpGain`, `Luck`, `Revival`.

Chaque personnage a ses **stats de base** (`BaseStats`) et un **passif** (§8.1).

### 5.2 Structure de code

```csharp
public enum StatType {
    MaxHealth, HealthRegen, MaxMana, ManaRegen,
    PhysicalAttack, MagicAttack, AttackSpeed, AttackRange, CritChance, CritDamage,
    ArmorPenFlat, ArmorPenPercent, MagicPenFlat, MagicPenPercent,
    Armor, MagicResist, Tenacity, SlowResist, MoveSpeed,
    Omnivamp, Lifesteal, HealShieldPower, AbilityHaste, ManaCostReduction
    /* + PickupRange, ExpGain, Luck... */ }

public enum ModifierType { Flat, PercentAdd, PercentMult }

public class StatModifier {
    public float Value; public ModifierType Type; public object Source; // Source = pour retirer en groupe
}

public class Stat {
    public float BaseValue;
    public float Value { get; }          // valeur finale, mise en cache
    public event Action<float> Changed;
    public void AddModifier(StatModifier m);
    public void RemoveModifier(StatModifier m);
    public void RemoveAllFromSource(object source);
}

public class StatController : MonoBehaviour {
    public Stat Get(StatType t);
    public void Init(StatBlock baseStats);
}
```

### 5.3 Formule de calcul d'une stat

```
Final = (Base + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult)
```

- `Flat` : +10 d'attaque.
- `PercentAdd` : tous les bonus % se **somment** (+10 % et +20 % → +30 %).
- `PercentMult` : se **multiplient** entre eux (réservé aux effets "puissants").
- Recalcul **lazy** (dirty flag) et event `Changed` pour l'UI/les systèmes réactifs.
- Clamp par stat (ex. `CritChance` ∈ [0,1], `MoveSpeed` ≥ 0).

### 5.4 Modificateurs temporaires
`TimedModifier` / `StatusEffect` : applique un modificateur pendant X secondes, gère stack/refresh, retire proprement (via `Source`). Base des buffs, debuffs, ralentissements, upgrades de level-up et objets.

> **En place** : `TimedModifiers` (`Character.Buffs`, tick chaque frame) : réappliquer depuis la même source sur la même stat **rafraîchit** (valeur + durée) au lieu de cumuler ; des sources différentes se cumulent. CC et ténacité : §5.7.

### 5.5 Croissance par niveau

Chaque personnage définit un `StatGrowth` (gain par niveau et par stat). Deux modes (`GrowthMode`) :
- **Linear** (par défaut) : `valeur = base + gain × (niveau − 1)`.
- **LoL** : `valeur = base + gain × (n−1) × (0.7025 + 0.0175 × (n−1))` (courbe non linéaire de LoL).

Appliquée comme `StatModifier` **Flat** dont la `Source` est `CharacterLevel`, **remplacé** à chaque niveau (jamais cumulé par erreur). Pour `AttackSpeed`, le gain est un **bonus %** (PercentAdd). Chaque entrée de `StatGrowth` porte son `ModifierType` (validation éditeur si `AttackSpeed` n'est pas en PercentAdd).

`ExperienceCurve` (SO unique, `Assets/Data/Progression`, enregistré dans le root) : XP à atteindre par niveau (niveau max = nombre d'entrées + 1, soit 18) et `LastSkillPointLevel` (15). Le niveau 1 donne déjà un point de compétence ; au-delà de 15, chaque niveau donne un point de **surcharge**. L'XP en trop passe au niveau suivant ; un gain peut faire monter plusieurs niveaux d'un coup.

### 5.6 Capacités (flags)

Certaines règles ne sont pas des stats mais des **capacités** accordées par un passif, un item ou un augment : `CapabilityController` tient un compteur par `Capability` et par source.

```csharp
public enum Capability { SpellCrit /*, ... */ }
// capabilities.Grant(Capability.SpellCrit, source);  capabilities.Has(Capability.SpellCrit);
```

Exemple : **les sorts ne peuvent pas critiquer par défaut** ; `SpellCrit` (passif de perso, augment du type « Gantelet précieux ») l'active.

> **En place (phase 8)** : `CapabilityController` (`Character.Capabilities`, un compteur de sources par capacité), `Capability.SpellCrit` lu par `SpellExecutor` (crit selon `CritChance`, dégâts × `CritDamage`). Augment « Gantelet serti ».

### 5.7 Contrôles (CC) et Ténacité

Types : `Stun`, `Root`, `Silence`, `Slow`, `Knockup`. `StatusEffect` calcule la durée effective :
```
duréeEffective = duréeBase × (1 − Tenacity)        // Tenacity clampée (ex. max 0.9)
```
S'applique aux CC **et aux ralentissements** (un Slow = `StatModifier` PercentAdd négatif sur `MoveSpeed`, retiré à l'expiration). `SlowResist` (optionnel) réduit en plus l'intensité du ralentissement.

> **En place (phase 9b)** :
> - **`StatusApplication`** (struct sérialisable, dans les données des sorts, passifs, items et augments) : `Kind` (`Stun`, `Root`, `Silence`, `Slow`, `Bleed`, `Burn`), `Duration`, `Power` (part de vitesse retirée pour un Slow, dégâts par seconde et par stack pour un DoT), `ScalingStat` + `Ratio` (DoT : + ratio × stat de celui qui l'applique), `MaxStacks`.
> - **`StatusEffects`** (C# pur, `Character.Status`, tické avec le perso, testé en EditMode) : Stun / Root / Silence durent `durée × (1 − Tenacity)`, un nouveau CC ne fait que **prolonger** le CC en cours ; **Slow** = modificateur **PercentMult** `−Power × (1 − SlowResist)` sur `MoveSpeed` via `TimedModifiers` (multiplicatif comme LoL, même source = rafraîchi), durée réduite par la ténacité ; **Bleed** (physique) / **Burn** (magique) : un DoT par (type, source), chaque application ajoute un stack (jusqu'à `MaxStacks`) et rafraîchit la durée, dégâts figés à l'application et infligés toutes les 0,5 s via `Character.TakeDamage` (crédités à l'applicateur : omnivamp, kills) ; la ténacité ne réduit pas les DoT. Tout est retiré à la mort et à l'`Init`.
> - **Effets** : Stun → pas de déplacement, d'attaque ni de sort, windup et incantation interrompus ; Root → pas de déplacement (attaques, sorts et Flash permis) ; Silence → pas de sort (Flash compris). `CharacterMovement.Immobilized` garde l'ordre en cours pour après (une ruée est arrêtée), `BasicAttacker` garde sa cible, `SpellCaster.Cast` refuse, `AbilityCaster` affiche « Étourdi » / « Réduit au silence » sans dépenser mana ni cooldown. `PatternRunner` ne lance pas d'étape pendant un stun du boss.
> - **Lisibilité** : icône du statut le plus fort à gauche de la barre de PV (`Sprites/Status`) et teinte du sprite (stun jaune et tremblant, root vert, silence violet, slow bleu, brûlure orange, saignement rouge).
> - Ténacité des ennemis : Brute 30 %, Grokk 50 %. *Reste : Knockup.*

---

## 6. Pipeline de dégâts

Tout dégât passe par un objet **`DamageInfo`** et par le `DamageReceiver` de la cible.

```csharp
public enum DamageType { Physical, Magic, True }

public struct DamageInfo {
    public float RawAmount;
    public DamageType Type;
    public bool IsCrit;
    public bool IsBasicAttack;       // pour le vol de vie
    public Character Source;         // peut être null (environnement)
    public float ArmorPenFlat, ArmorPenPercent, MagicPenFlat, MagicPenPercent; // snapshot des stats de l'attaquant
}
```

### Étapes
1. **Calcul côté attaquant** : `Raw = Attack × ratio du sort ou de l'attaque` (sorts : généralement `MagicAttack`).
2. **Critique** : `if (PeutCritiquer && Random < CritChance) Raw *= CritDamage`. Les **attaques de base** peuvent toujours critiquer ; les **sorts** seulement si la capacité `SpellCrit` est active (§5.6).
3. **Résistance effective de la cible** (type de dégât) :
   ```
   effective = max(0, resistance × (1 − penPercent) − penFlat)
   ```
4. **Multiplicateur de réduction** :
   ```
   multiplier = 100 / (100 + effective)       // résistance négative gérée via 2 − 100/(100 − r)
   Final = Raw × multiplier
   ```
   `True` ignore les résistances.
5. **Application** : bouclier d'abord, puis PV. Event `Damaged`, `Died`.
6. **Sustain côté attaquant** (après application, sur les dégâts **réellement infligés**) :
   ```
   heal = Final × Omnivamp  +  (IsBasicAttack ? Final × Lifesteal : 0)
   heal *= HealShieldPower
   ```
7. **Feedback** : texte de dégâts (pool), flash blanc, hit-stop léger, SFX.

> `HealShieldPower` s'applique à **tous les soins et boucliers produits** par le perso, y compris sur lui-même (omnivamp, lifesteal, sorts de soin).

> **En place** : `DamageInfo` (struct), `DamageCalculator` (formules pures, testées), `Health` (PV liés à `MaxHealth` : un gain de max ajoute autant de PV courants, une baisse les borne ; régénération `HealthRegen`/s ; mort ; résurrection), `Character.TakeDamage` (résistances → PV → sustain de l'attaquant). Chiffres de dégâts (`DamageNumbersView`) : jaunes, plus gros et suivis de « ! » en crit, rouges quand le joueur est touché. **Boucliers (phase 9b)** : `Health.AddShield(montant, durée)` ; les boucliers absorbent avant les PV (celui qui expire le premier d'abord), disparaissent à expiration, à la mort et à la résurrection ; les dégâts absorbés comptent comme subis (omnivamp, chiffres). Affichés en blanc après les PV (barre du HUD avec « (+N) », barres au-dessus des têtes), `PlayerHealthChangedEvent.Shield`. **Soins / boucliers produits** : `Character.ReceiveHeal` / `ReceiveShield` × `HealShieldPower` du producteur (1 par défaut quand le `StatBlock` ne la définit pas). *Reste : flash blanc, hit-stop, SFX.*

### Cooldowns & Ability Haste
```
cooldownEffectif = cooldownBase × 100 / (100 + AbilityHaste)
```
S'applique au délai de chaque sort du champion (§7.4) ; les sorts d'invocateur l'ignorent. Le coût en mana n'est **pas** affecté par le haste.
`AttackSpeed` : `intervalle = 1 / (AttackSpeedBase × AttackSpeedStat)`.

---

## 7. Sorts de champion (modèle LoL)

> **Refonte du 13/10/2026** : la mécanique d'éléments (empiler A/Z/E puis lancer avec R, 19 recettes par perso) est **abandonnée** au profit du modèle de League of Legends : **chaque champion a ses propres sorts** Q W E R, un passif et deux sorts d'invocateur. Personnage d'essai : **Ezreal**.

### 7.1 Principe et touches

| Touche (position QWERTY) | AZERTY | Action |
|---|---|---|
| **Q / W / E** | A / Z / E | Sorts de base du champion (5 rangs) |
| **R** | R | Ultime (3 rangs) |
| **D / F** | D / F | Sorts d'invocateur (F = Flash ; D = sort d'invocateur ramassé, §9.5) |
| **Ctrl + Q / W / E / R** | Ctrl + A / Z / E / R | Dépense un point de compétence sur le sort (aussi : bouton « + » du HUD) |

- Les touches sont liées **par position** (comme LoL) : un AZERTY joue sur A Z E R, un QWERTY sur Q W E R (§4.7).
- **Mode de lancement** (option du joueur, §4.7) : *Normal* (visée puis clic gauche), *Rapide avec indicateur* (défaut : maintenir = portée et forme affichées, relâcher = lancer vers le curseur), *Rapide* (lancer immédiat). Clic droit / Échap annulent une visée. Les visées sont exclusives (attack-move, un sort à la fois : `AimModeEvent`).
- Un **stun / silence** bloque les sorts (§5.7).

### 7.2 Données : `SpellDefinition`

Un seul type de donnée pour tous les sorts (champion, invocateur, ennemis, boss). Les valeurs par rang sont des **tableaux** indexés par rang − 1 (la dernière valeur sert au-delà ; un sort sans table de rangs a un seul rang) :

| Champ | Rôle |
|---|---|
| `RankUnlockLevels` | niveau du champion requis par rang : **Q W E = 1/3/5/7/9**, **R = 6/11/16** ; vide = un seul rang (invocateurs, ennemis) |
| `BaseDamage[]`, `Cooldowns[]`, `ManaCosts[]` | dégâts de base, délai de récupération et coût en mana par rang |
| `AdRatio`, `BonusAdRatio`, `ApRatio` | dégâts = base(rang) + `AdRatio` × AD + `BonusAdRatio` × AD bonus + `ApRatio` × AP |
| `Targeting`, `Range`, `Radius`, `ConeAngle`, `CastTime`, `CanMoveWhileCasting` | visée et incantation (§7.5) |
| `Kind` (Magic / Physical / Buff), `Buffs`, `Statuses`, `SelfSustain`, `AffectsAllies` | effet (§7.6, §5.7) |
| `ProjectileSpeed`, `Pierce`, `Blink`, `FixedRange` | projectile, traversée, téléportation |
| `AppliesOnHit` | chaque impact compte comme une attaque de base pour les passifs (effets à l'impact) |
| `CooldownRefundOnHit` | chaque impact réduit les délais de récupération du lanceur (Ezreal Q : 1,5 s) |
| `Mark` (`Enabled`, `Duration`, `ManaRefund[]`) | le projectile **marque** au lieu de blesser ; la prochaine attaque ou un autre sort du lanceur sur la cible fait exploser la marque (dégâts du sort + mana rendu) — Ezreal W |
| `BlinkBoltRadius` | après un blink, trait sur l'ennemi le plus proche (marqué en priorité) — Ezreal E |
| `Description`, `Icon`, `Color` | UI, indicateurs, VFX |

L'**AD bonus** = AD total − (base + croissance par niveau) : `Character.BonusStat` (sert aussi à la force adaptative).

### 7.3 Mana et coût

Coût = `ManaCosts[rang]` × (1 − `ManaCostReduction`). Le mana est débité à la confirmation ; refusé s'il manque (« Mana insuffisant »). Les sorts d'invocateur ne coûtent rien.

### 7.4 Lanceur du joueur : `AbilityCaster`

Composant du variant `Player`, 6 emplacements (`AbilitySlots` : Q W E R D F) :
1. touche pressée (`AbilityKeyEvent`) → vérifications : sort présent, **appris** (rang ≥ 1), délai, mana, incantation en cours, CC ;
2. visée via `SpellTargeting` selon le mode de lancement (ou lancement immédiat d'un sort `Self`) ;
3. confirmation → mana payé, `SpellCaster.Cast(sort, contexte, rang)`, délai = `Cooldowns[rang]` × 100 / (100 + `AbilityHaste`) (les invocateurs ignorent le haste).
- `CharacterEvents.CooldownRefund` réduit les délais des 4 sorts (Ezreal Q).
- Publie `AbilitiesChangedEvent` (sorts, rangs, « + » possibles, points) et `CooldownStartedEvent(slot, durée)` pour le HUD.

### 7.5 Visée

`SpellTargeting` (variant `Player`) dessine la portée et la forme sous le curseur : rectangle d'un skillshot, cercle d'une zone, secteur d'un cône, anneau sous la cible d'un sort ciblé. `SpellExecutor` (scope Game, joueur et ennemis) applique l'effet selon `Targeting` : **Skillshot** (`SkillshotProjectile`, premier ennemi ou traversée), **GroundPoint** (zone, ou **blink** vers le point marchable le plus lointain de la ligne), **Cone**, **UnitTarget** (projectile à tête chercheuse), **Self**. Les sorts ennemis sont télégraphiés pendant leur incantation (§9.6).

### 7.6 Effets

- **Magic / Physical** : contre `MagicResist` / `Armor`, pénétration du lanceur ; crit **uniquement** avec la capacité `SpellCrit` (§5.6) ; omnivamp oui, lifesteal non.
- **Buff** : `BuffEffect` → `Character.Buffs` (Source = le sort : relancer rafraîchit) ; `SelfSustain` soin / bouclier (+ `PowerRatio` × AP, × `HealShieldPower`) ; `AffectsAllies` les donne aussi aux alliés dans `Radius`.
- **À l'impact** : statuts (§5.7), marque posée ou déclenchée (`SpellExecutor.TryDetonateMark`, aussi appelé par `BasicAttacker`), `CharacterEvents.SpellHit` (passifs), effets à l'impact (`AppliesOnHit`), réduction de délais (`CooldownRefundOnHit`).
- La marque est un état de `StatusEffects` (une à la fois, avec le sort et le rang qui l'ont posée) ; l'icône du sort marqueur s'affiche à côté de la barre de PV.

### 7.7 Rangs et points de compétence

- **1 point de compétence à chaque niveau, de 1 à 18** (`ExperienceCurve.LastSkillPointLevel = 18`) : 5 + 5 + 5 + 3 = 18.
- `AbilityRanks` (C# pur, testé) : un rang n'est accessible qu'au niveau écrit dans le sort (Q W E : rang n au niveau 2n − 1 ; R : 6 / 11 / 16) ; un sort au rang 0 ne peut pas être lancé. Refus expliqués (« Disponible au niveau 6 », « Rang maximum », « Aucun point de compétence »).

### 7.8 Ezreal (personnage d'essai)

Valeurs de LoL (portées et vitesses / 100), `EzrealSetup` :

| Sort | Effet |
|---|---|
| **Passif — Sursaut de puissance** | chaque sort qui touche donne +10 % vitesse d'attaque pendant 6 s, cumulable 5 fois (stacks `PerSpellHit`) |
| **Q — Tir mystique** | skillshot (portée 12) : 20/45/70/95/120 + 130 % AD + 15 % AP physiques ; effets à l'impact ; toucher réduit ses délais de 1,5 s ; 5,5 → 4,5 s ; 28 → 40 mana |
| **W — Flux d'essence** | skillshot qui marque 4 s ; une attaque ou un autre sort fait exploser la marque : 80/135/190/245/300 + 60 % AD bonus + 70 % AP magiques, et rend 60 → 100 mana ; 8 s ; 50 mana |
| **E — Transfert arcanique** | blink (4,75) puis trait sur l'ennemi le plus proche (marqué en priorité, rayon 7,5) : 80/130/180/230/280 + 50 % AD bonus + 75 % AP ; 26 → 14 s ; 90 mana |
| **R — Barrage de précision** | 1 s d'incantation, vague large (rayon 1,6) qui traverse tout sur 30 : 350/550/750 + 100 % AD bonus + 90 % AP ; 120/105/90 s ; 100 mana |
| **F — Flash** | blink de longueur fixe (4,5) vers le curseur, 20 s |

Stats : 600 PV (+102), 375 mana (+70), 62 AD (+2,5), 0,625 AS (+2,5 %), portée 5,5, 24 armure (+4,7), 30 RM (+1,3), régénération de mana **3/s (+0,15)** au lieu de 1,7 : les hordes demandent plus de sorts qu'une phase de lane (provisoire).

### 7.9 UI

`PlayerHud` + `AbilityBarView` : Q W E R (icône, touche affichée selon la disposition, pastilles de rang, « + » cliquable quand un point peut y aller, voile tant que le sort n'est pas appris), puis D F ; délais en voile sombre + secondes restantes. Barres de PV (bouclier), mana et XP, niveau, gold. Infobulles et refonte façon LoL : phase suivante.

## 8. CharacterDefinition (le "preset" de perso)

```csharp
[CreateAssetMenu(menuName = "Game/Character Definition")]
public class CharacterDefinition : ScriptableObject {
    public string DisplayName;
    public Sprite Sprite;                    // ou RuntimeAnimatorController
    public Vector2 ColliderSize;

    public StatBlock BaseStats;              // PV, mana, attaque, vitesse d'attaque, portée, vitesse...
    public StatGrowth GrowthPerLevel;        // gain par stat et par niveau
    public BasicAttackDefinition BasicAttack;

    public SpellDefinition[] Abilities;      // Q, W, E, R (§7)
    public SpellDefinition SummonerD, SummonerF;   // sorts d'invocateur (F = Flash)
    public PassiveDefinition Passive;        // passif du perso (§8.1)

    public AIBehaviorDefinition Ai;          // null pour le joueur
    public Faction Faction;                  // Player / Enemy / Neutral
}
```

`Character.Init(CharacterDefinition def)` configure le prefab générique :
sprite, collider, stats, croissance, attaque de base, sorts, passif, comportement. **Un prefab, N personnages.**

> **En place (phase 3)** : `CharacterDefinition` porte `DisplayName`, `Faction`, `BaseStats` (`StatBlock`), `GrowthPerLevel` (`StatGrowth`) et `BasicAttack` (`BasicAttackDefinition`). Ainsi que `Sprite` et `BodySize` (corps physique et agent). Les autres champs arrivent avec leur phase (éléments 5b, sorts 5c, passif 8, IA 6).
> - `Character.Init` : sprite et corps → `StatController.Init(BaseStats)` → `CharacterLevel` (applique la croissance) → `CharacterMovement.Init(stat MoveSpeed)`. Ré-appelable (pooling, nouvelle run).
> - Seul le **personnage joueur** parle au bus global : il écoute `XpGrantedEvent` et publie `XpChangedEvent`, `LevelUpEvent` et `StatChangedEvent` (snapshot complet à l'`Init`). Les ennemis n'y publient rien.
> - Joueur de `TestScene` : prefab `Player` + `Char_TestHero` (`Assets/Data/Characters/TestHero/`), spawné par `PlayerSpawner` (§3). Valeurs de stats **provisoires**.
> - `CharacterDebugPanel` (TestScene, IMGUI) : niveau, XP, points, stats principales, boutons « +100 XP » et « Level up » ; ne passe que par le bus.

### 8.1 Passif

Chaque personnage a **un passif**, défini par un `PassiveDefinition` (SO) qui fabrique une instance runtime `Passive` (même séparation données/runtime que les sorts). **Les items et les augments utilisent le même mécanisme** (§9.3, §9.4).

- Le passif s'abonne aux événements du personnage via un hub `CharacterEvents` : `BasicAttackHit`, `SpellCast`, `DamageDealt`, `DamageTaken`, `Kill`, `LevelUp`, `ElementStackAdded`, `HealthThreshold`…
- Ses effets passent par les systèmes existants : `StatModifier` (§5), capacités (§5.6), statuts (saignement, ralentissement…), dégâts/soins via `DamageInfo`.
- Il **évolue avec le niveau** : valeurs `Base + PerLevel × (niveau − 1)` (comme `SpellScaling`), éventuellement des paliers.

**Trois familles à supporter :**

| Famille | Exemple |
|---|---|
| **Statique** | +50 % de chance de critique ; capacité `SpellCrit` |
| **Déclenchée** | les attaques de base appliquent un **saignement** ; un sort sur trois donne +MoveSpeed 3 s |
| **À stacks** | **Mal phénoménal** (Veigar : +1 `MagicAttack` par stack, permanent) ; **Enthousiasme** (Jinx) / **rythme critique** (stacks remis à zéro en fin de vague) |
| **Conditionnelle** | gagner de l'AD / AP selon le **nombre d'ennemis autour** (valeur recalculée, sans stacks de sorts) |

**Brique commune `StackCounter`**, configurée **par passif** sur deux axes :

```csharp
public enum StackSource { PerSpellCast /* 1 par sort qui touche */, PerTargetHit, PerKill, PerBasicAttack, Continuous /* valeur calculée */ }
public enum ResetPolicy { Never /* toute la run */, EndOfWave, EndOfStage, AfterDuration }
```

- Les passifs **ne sont pas obligés de stacker sur les sorts** : `Continuous` recalcule périodiquement une valeur (ex. nombre d'ennemis dans un rayon) et met à jour un `StatModifier` (Source = le passif).
- À chaque changement, le passif met à jour ses `StatModifier` ou un multiplicateur lu par les sorts. Événement `Changed` pour l'UI.

> **En place (phase 8)** :
> - **`CharacterEvents`** (`Character.Events`) : `BasicAttackHit`, `DamageDealt`, `DamageTaken`, `Kill`, `SpellCast`, `LevelUp`, levés par `Character.TakeDamage`, `BasicAttacker` et `SpellCaster`.
> - **`PassiveDefinition`** (SO, aucune ligne de code par passif) = briques combinables : `Stats` (statique), `Capabilities`, `Triggers` (`On` = BasicAttackHit / SpellCast / Kill / DamageTaken, `EveryN`, `Chance`, `MinSpellStacks` ; effets `TimedStats`, `BonusDamage` = base + ratio × stat, `DropLoot`), `Stacks` (`StackSource` PerBasicAttack / PerSpellCast / PerKill, `ResetPolicy` Never / EndOfWave / AfterDuration, `Max`, `PerStack`), `NearbyEnemies` (bonus continu par ennemi dans un rayon, recalculé toutes les 0,25 s).
> - **`Passive`** (runtime : chaque `StatModifier` a pour Source l'instance, retiré proprement) et **`PassiveController`** (`Character.Passives` : passif du perso ajouté par `Init`, puis items et augments ; tick par le perso ; `EndOfWave` au `Reward`).
> - **Héros** : « Surcharge arcanique » (une attaque sur trois, 20 + 35 % PM) jusqu'à la refonte LoL ; **Ezreal** a maintenant « Sursaut de puissance » (§7.8, stacks `PerSpellHit`). **Items** : « Salve » (Arc rapide, 1 attaque sur 4) et « Inspiration » (Couronne du mage, +20 PM 3 s après un sort ; avant : un sort à 2 éléments).
> - **Phase 9b** : événement `SpellHit` (chaque cible touchée par un sort) ; effets `ApplyStatus` (sur la cible d'une attaque ou d'un sort), `Heal` et `Shield` (sur le porteur : `Amount` + `AmountRatio` × `AmountStat`, ex. % des PV max) ; conditions `HealthBelow` (PV du porteur sous un seuil) et `Cooldown` (recharge interne) sur chaque trigger.
> - **Objets de LoL (13/10/2026)** : effets `Bolts` (N traits à tête chercheuse sur les ennemis les plus proches de la cible : Runaan, Luden, Shiv statique), `Splash` (dégâts autour de la cible : Hydres), `ReduceCooldowns` (Navori, bottes ioniennes), `Charge` + `ConsumesCharge` (lame enchantée : un sort charge la prochaine attaque, Force de la trinité, Fléau de liche, Faucheur d'essence) ; dégâts en % des PV **max** ou **actuels** de la cible (`TargetMaxHealthRatio`, `TargetCurrentHealthRatio` : Lame du roi déchu, Salutations de Dominik) ; brûlures en % des PV max de la cible (`StatusApplication.TargetMaxHealthRatio` : Liandry) ; `DamageTaken` a l'**attaquant** pour cible (Cotte épineuse, Présage de Randuin) ; `Aura` (dégâts réguliers autour du porteur, soin par ennemi : Égide du feu solaire, Désespoir éternel) ; `Conversions` (part d'une stat, bonus ou totale, ajoutée à une autre : Bâton de l'archange, Armure de Warmog, Jugement d'Atma). Traits tirés par `Character.FireBolt`.
> - *Reste : valeurs qui évoluent avec le niveau (`Base + PerLevel`).*

---

## 9. Boucle rogue-lite, économie et ennemis

### 9.1 Run, stages et vagues

Une **run** est une partie sur un **stage**, **équivalent d'un niveau de BTD6**. Un stage = une suite de **vagues** (`WaveDefinition`) avec boss à intervalles, puis des vagues bonus. Le personnage repart du **niveau 1 sans rien** à chaque run ; seule la méta-progression persiste (§9.7).

```
 Vague (timer T, quota Q)
   │  quota atteint : plus d'XP, les ennemis continuent de spawn (gold seul)
   ▼
 Fin de vague = timer écoulé ET quota atteint
   ▼
 Événement de fin de vague selon le WaveRewardSchedule (§9.3) : augment, enclume prismatique gratuite, boutique légère ou complète
   ▼  (jeu en pause)
 fermeture ──► compte à rebours 10 s ──► vague suivante
```

- **Vague** : un **timer** et un **quota d'ennemis à éliminer**. Les ennemis spawnent en continu. Une fois le quota atteint, **l'XP n'est plus donnée** mais les ennemis continuent d'apparaître et de **donner du gold** jusqu'à la fin du timer.
- **Fin de vague** : **timer écoulé ET quota atteint**. Les ennemis restants disparaissent et l'**événement de fin de vague** prévu par le `WaveRewardSchedule` s'ouvre (§9.3).
- **Pas de temps d'attente entre les vagues et aucun soin.** La **régénération** de PV et de mana (stats) travaille **pendant la vague** : le joueur doit **esquiver** et gérer ses ressources.
- **Vies de stage** : **3 vies** (`StageLives`, modifiable par la difficulté ou des passifs).
  - Si le **timer s'écoule sans que le quota soit atteint**, le joueur **perd 1 vie** mais **la vague continue** jusqu'à ce que le quota soit rempli.
  - **30 s après chaque perte de vie** (`LifeLossInterval`), il **reperd une vie** tant que la vague n'est pas terminée.
  - **0 vie = game over** : il peut **rejouer le stage depuis la vague 1** avec son personnage ou **retourner au menu**.
- **Écrans de fin de vague** (augment, enclume prismatique gratuite, boutiques) : **le jeu est en pause** (`Time.timeScale = 0`). Quand le joueur les ferme, la **vague suivante se lance 10 s plus tard** (`PreWaveCountdown`, non pausé).
- **Vagues bonus** : une fois **toutes les vagues du stage terminées**, un **nombre fixe** de vagues bonus (`BonusWaveCount`, difficulté croissante, façon BTD6) peut se jouer. **En vagues bonus, la boutique complète s'ouvre après chaque vague, avec un choix d'augment toutes les deux vagues.** Après la dernière, **le joueur gagne** : **full clear** du stage et gain de monnaie méta (§9.7). Les enclumes à stats gagnent alors en prix et en poids de rareté (§9.3).
- **Quitter et reprendre (façon BTD6)** : le joueur peut **quitter n'importe quelle vague à tout moment** ; la progression est sauvegardée et la reprise se fait **au début de la vague en cours** (`RunSnapshot`, §9.7).
- Le temps de complétion d'un stage **n'a aucune importance** (pas de score de vitesse).
- `WaveDefinition` : `KillQuota`, `TimeLimit`, tables de spawn, `PostQuotaSpawnRate`, `WaveScaling`, boss éventuel.

> **En place (phase 7a)** :
> - **Données** : `StageDefinition` (vagues, vagues bonus, `WaveRewardSchedule`, `Lives` = 3, `LifeLossInterval` = 30 s, `PlayerDeath`, `FirstWaveCountdown` = 5 s, `PreWaveCountdown` = 10 s), `WaveDefinition` (`KillQuota`, `TimeLimit`, `SpawnTable`, `SpawnInterval`, `PostQuotaSpawnInterval`, `MaxAlive`, `WaveScaling` = multiplicateurs de PV et de dégâts appliqués en `PercentMult`). Stage de test `Stage_Test` (`Assets/Data/Run/TestStage`) : **5 vagues + 2 vagues bonus**, quotas 8 → 28, 60 à 90 s, gobelins d'abord puis archers et brutes, PV ×1 → ×1,8 (valeurs provisoires, rééquilibrées après le premier test : les quotas d'origine étaient intenables).
> - **`RunDirector`** (scope Game) pilote la `RunStateMachine` (objet de la scène, règles écrites par *Project Setup*) : `PreWave` (compte à rebours, rien ne spawn) → `Combat` (`HordeSpawner.Configure` avec la vague, spawn continu) → `Reward` si timer écoulé **et** quota atteint, sinon `Overtime` (−1 vie tout de suite, puis toutes les 30 s ; quota atteint → `Reward`). Fin de vague : la horde disparaît, `WaveRewardEvent` annonce la récompense du `WaveRewardSchedule`, le jeu est en pause (`TimeScaleService`, map UI). « Continuer » (`RewardClosedEvent`) → compte à rebours de la vague suivante. Dernière vague bonus terminée → `RunEndedEvent(won)` → `GameState.Win` ; 0 vie → `GameState.GameOver`.
> - **Quota et XP** : seuls les ennemis du `HordeSpawner` comptent (`WaveEnemyKilledEvent`, pas les mannequins). L'XP de kill passe désormais par le `RunDirector` (`EnemyKilledEvent` → `XpGrantedEvent`), coupée une fois le quota atteint ; le gold reste.
> - **Rééquilibrage Ezreal (13/10/2026)**, mesuré avec le bot `BalanceSimulation` (test PlayMode `[Explicit]`, `-testFilter BalanceSimulation` : il joue Ezreal sur tout le stage — kite, Q / W sur le plus proche ou le boss, E et Flash en fuite, R dans les groupes, build d'Ezreal en boutique — et écrit une ligne par vague : temps du quota, PV minimum, niveau, or, items) : Ezreal est un tireur mono-cible, la horde de l'ancien héros à sorts de zone le submergeait (≈ 0,5 kill/s contre 0,8 spawn/s). Vagues : spawns espacés ×1,6, `MaxAlive` ×0,6, quotas ×0,8 (vague 1 : 6 kills, 7 ennemis max) ; **XP des ennemis ×4,5** (un kit à rangs a besoin du niveau : ≈ niveau 9 au boss), **or ×2,5** (objets de LoL à ~300 or) ; Grokk 3000 → 2000 PV, armure 45 → 35. Résultat du bot : vagues 1 à 4 tenues (PV minimum 20 à 99 % selon les runs), le boss reste dur pour lui (il n'esquive pas et la boutique complète n'arrive qu'après la vague 5). Les tables des scripts de setup gardent les valeurs d'origine (création) : les assets font foi.
> - **Mort du joueur** : **game over immédiat** (`StageDefinition.PlayerDeath = GameOver`, validé). L'autre règle (`LoseLife` : une vie, la horde disparaît, respawn au spawn après 3 s) reste disponible par stage ; le respawn sert aussi au bac à sable quand la run est arrêtée.
> - **UI** : `RunHudView` en haut (vague / bonus, timer ou compte à rebours, kills / quota, vies, bannières « Vague N », « Vie perdue »), les écrans de récompense (boutique, enclume depuis la 7b ; augment depuis la phase 8, qui a remplacé l'écran d'attente `WaveRewardPanel`), `RunEndPanel` (Victoire / Défaite d'après l'état de jeu `Win` / `GameOver`, vagues terminées, niveau, or ; Rejouer = la vague 1 de nouveau, Menu principal, Quitter).
> - **Debug** : bouton « Skip wave » du panneau de test (`SkipWaveRequestedEvent`).
> - *Reste (7b) : boutiques, élixirs, enclumes, items, `Inventory`. Plus tard : augments (phase 8), boss, `RunSnapshot`.*

### 9.2 XP, niveaux, points de compétence et gold

- **XP et gold sont crédités directement au kill** (`EnemyDefinition.XpReward`, `GoldReward`). Une fois le quota de la vague atteint, l'XP n'est plus donnée (gold seul).
- **XP** : `ExperienceCurve` (SO). À chaque niveau : croissance des stats (§5.5) et évolution du passif (§8.1). **+1 point de compétence à chaque niveau, de 1 à 18** (rangs des sorts, §7.7). Pas de pause ni de choix imposé à la montée de niveau.
- **Niveau max en partie : 18** (`MaxLevel`).
- **Gold** : `Wallet` (valeur, events), dépensé en boutique.

> **En place (phase 6)** : XP au kill via `EnemyKilledEvent` (joueur), gold via `Wallet` (scope Game, remis à 0 à chaque run) → `GoldChangedEvent` → HUD (« N or » à droite des barres). *Reste : quota de vague (XP coupée), boutique (phase 7).*

### 9.3 Items, boutique et enclumes

```csharp
public enum ItemTier { Legendary, Prismatic }
public enum RarityTier { Silver, Gold, Prismatic }

[CreateAssetMenu(menuName = "Game/Item")]
public class ItemDefinition : ScriptableObject {
    public string DisplayName; public Sprite Icon; public int Cost; public ItemTier Tier;
    public StatModifierData[] Stats;        // ex. MaxMana +300, ManaRegen, AttackSpeed %…
    public PassiveDefinition Passive;       // optionnel (même système que le passif du perso)
    public string[] Tags; public bool Unique;
}

public enum AnvilKind { Stat, Item }

[CreateAssetMenu(menuName = "Game/Anvil")]
public class AnvilDefinition : ScriptableObject {
    public AnvilKind Kind; public Sprite Icon;
    public float[] RarityWeights;           // Kind = Stat : chances Silver / Gold / Prismatic ; UNE rareté est tirée à l'ouverture
    public ItemTier ItemRarity;             // Kind = Item : Legendary ou Prismatic (fixe)
    public int Cost;                        // prix FIXE pour toute la partie
    public int ChoiceCount = 3;             // toujours 3 choix
    public int RerollsPerChoice;            // 0 pour les enclumes à stats, 1 pour les enclumes à items
    public bool OnlyAfterStageCleared;      // variante plus chère, avec de meilleurs poids de rareté
}

public class StatChoiceDefinition : ScriptableObject {   // un choix possible d'enclume à stats
    public RarityTier Rarity; public StatModifierData[] Stats;   // bonus permanent
}

[CreateAssetMenu(menuName = "Game/Elixir")]
public class ElixirDefinition : ScriptableObject {   // consommable acheté en boutique
    public string DisplayName; public Sprite Icon; public int Cost;
    public StatModifierData[] Stats;        // bonus temporaire (StatModifier avec durée, §5.4)
    // dure jusqu'à la fin de la vague qui suit l'achat
}
```

- **Inventaire : 6 slots** comme LoL (`Inventory`). Stats d'item = `StatModifier` (Source = l'instance d'item), passifs via `PassiveController`.

**Ordre des fins de vague** (`WaveRewardSchedule`, SO piloté par les données : un prélude, puis une boucle) :

```
 Vague 1  → fin : Augment
 Vague 2  → fin : Enclume à items prismatique (gratuite)
 ┌─ boucle ───────────────────────────────────────────────┐
 │ Vague → fin : Boutique légère (élixirs + enclumes à stats) │
 │ Vague → fin : Augment                                      │
 │ Vague → fin : Boutique complète                            │
 └────────────────────────────────────────────────────────┘
```

| Événement | Contenu |
|---|---|
| **Augment** | draft d'augments (§9.4), **toutes les 3 vagues** |
| **Enclume prismatique gratuite** | comme en Arena : le joueur reçoit **gratuitement une enclume à items prismatique** (une seule fois, après la vague 2) : **3 choix**, **1 reroll par choix** |
| **Boutique légère** | **élixirs** et **enclumes à stats** |
| **Boutique complète** | boutique légère + **tous les items légendaires** + **enclumes à items** (légendaire, prismatique) ; **en vagues bonus, après chaque vague** |

**Limites d'achat par boutique**
- **Un seul élixir** : il **s'active immédiatement** et **dure jusqu'à la fin de la vague qui suit**.
- **Un seul item (boutique complète)** : les **enclumes à items comptent dans cette limite**. Elles permettent de **dépenser moins** et de **tenter sa chance** (par exemple pour garder du gold pour une enclume à stats ou un élixir).
- **Enclumes à stats** : quantité **libre**.

**Enclumes**
- **Une enclume ouvre toujours 3 choix de la même rareté.**
  - **Enclume à stats** : à l'ouverture, **une rareté est tirée** (Argent, Or ou Prismatique) selon les `RarityWeights` de l'enclume ; les 3 choix sont des bonus de stats permanents de cette rareté. **Pas de reroll.**
  - **Enclume à items** : rareté **fixe** (**légendaire** ou **prismatique**), 3 items au choix, **1 reroll par choix**.
- **Toutes les raretés existent dès le début** pour les enclumes à stats. Après le **clear du stage** (toutes les vagues terminées, vagues bonus comprises), les enclumes à stats **coûtent plus cher** et ont **de meilleurs poids de rareté** : c'est la **seule** chose que le clear débloque.
- **Enclumes à items** (disponibles **avant le clear**) : l'enclume légendaire coûte **un peu moins** qu'un item légendaire, l'enclume prismatique **presque le double**. Les items prismatiques ne s'obtiennent que par ce biais, dont l'enclume prismatique gratuite du début.
- **Prix fixes** pour toute la partie. Boutique d'items : **pas de composants**.
- Items de mana (`MaxMana`, `ManaRegen`, `ManaCostReduction`) pour les builds qui en ont besoin.

> **En place (phase 7b)** :
> - **Données** (`Assets/Data/Economy`, générées par `EconomySetup`, noms originaux, valeurs provisoires) : `ItemDefinition` (8 légendaires 240–280 or, 4 prismatiques via enclumes seulement), `ElixirDefinition` (force, sorcellerie, fer : 40 or), `StatChoiceDefinition` (7 Argent, 7 Or, 6 Prismatique), `AnvilDefinition` (enclume à stats 60 or, poids 70 / 25 / 5, sans reroll ; légendaire 220 or et prismatique 480 or, 1 reroll par choix), `ShopCatalog` (tout ce qui est en vente + l'enclume gratuite). Bonus = `StatBonus` (stat, type, valeur). Icônes pixel-art dans `Sprites/Items`.
> - **`RewardController`** joue les récompenses de fin de vague une par une (`RewardStepEvent` → `RewardStepDoneEvent`), puis `RewardClosedEvent`. Augment : écran d'attente (phase 8).
> - **`ShopService`** (requêtes par le bus, réponses en instantanés `ShopStateEvent` / `AnvilOfferEvent` avec, pour chaque offre, la raison du refus) : limites par boutique (1 élixir ; 1 item, enclumes à items comprises ; enclumes à stats libres), inventaire plein, items uniques, enclume à stats (rareté tirée, 3 choix de cette rareté), enclume à items (3 items non possédés du rang, 1 reroll par choix), enclume prismatique gratuite après la vague 2.
> - **`Inventory`** (6 slots) et **`PlayerUpgrades`** : chaque bonus est un `StatModifier` dont la `Source` est ce qui l'a donné (copie d'item, choix d'enclume, élixir) ; l'élixir disparaît au `Reward` qui suit sa première vague.
> - **UI** : `ShopPanel` (or, limites, inventaire, cartes grisées avec la raison), `AnvilPanel` (au-dessus de la boutique ; Choisir, Relancer, Passer si l'inventaire est plein), `InventoryView` (6 slots + élixir actif en bas à droite du HUD).
> - **Phase 9b** (`ContentSetup`) : **+12 légendaires et +2 prismatiques** (26 items en tout), la plupart avec un passif : Lame hémorragique (saignement), Sceptre glacial (sorts qui ralentissent), Marteau-tonnerre (1 attaque sur 5 étourdit), Talisman du brasier (sorts qui brûlent), Bouclier du gardien (bouclier sous 35 % PV, 30 s), Bottes de vif-argent (ténacité), Calice de vie (soin au kill, puissance des soins), Grimoire runique, Lance du chasseur, Cœur de troll, Sablier runique, Crocs du vent (vitesse au kill) ; prismatiques Couronne du roi gobelin (stun + bouclier) et Orbe de l'éclipse (brûlure).
> - **Objets de LoL (13/10/2026, `LolItemsSetup`)** : les objets de test sont remplacés par **65 objets recopiés de LoL** (stats de LoL + **un passif propre à chacun**, simplifié quand il dépend d'un actif ou d'un autre champion ; prix de LoL / 10 ; vitesse +45 → +0,7) : 16 de tireur / AD (Lame d'infini, Tueur de krakens, Ouragan de Runaan, Shiv statique, Lame du roi déchu, Lame enragée de Guinsoo…), 12 de mage (Coiffe de Rabadon, Compagnon de Luden, Tourment de Liandry, Bâton du vide, Fléau de liche…), 10 de combattant (Force de la trinité, Hydres, Gage de Sterak…), 11 de tank (Égide du feu solaire, Cotte épineuse, Armure de Warmog…), **6 paires de bottes** et **10 prismatiques façon Arena** avec **force adaptative** (Couronne du roi démon, Paratonnerre, Masque jumeau…). Icônes pixel-art (cadre or : légendaire, gris : bottes, violet : prismatique). **Groupes uniques** (`ItemDefinition.UniqueGroup`) : une seule paire de bottes, une seule lame enchantée, une seule hydre, une seule ligne de vie (refus « Un seul objet « Bottes » »). Élixirs aux noms de LoL. Grokk porte l'Égide du feu solaire et le Couperet noir.
> - *Reste : vente d'items, prix et poids après le clear du stage (méta), équilibrage.*

### 9.4 Augments (façon Arena)

- `AugmentDefinition` (SO) : nom, icône, **rareté** (Argent / Or / Prismatique), effets = `PassiveDefinition` et/ou `StatModifierData`, **capacités accordées**, tags/prérequis. Le système est **générique** : n'importe quel effet exprimable par ces briques est un augment valide.
- **4 slots d'augments** de base (`AugmentSlots`), **augmentables jusqu'à 6** (`MaxAugmentSlots`).
- **Draft** : **toutes les 3 vagues**, selon le `WaveRewardSchedule` (§9.3), **1 augment parmi 3** (jeu en pause). Les **3 choix sont de la même rareté de base**.
- **Reroll** : **1 reroll de base par slot d'augment de base** (`RerollsPerDraft`), augmentable par passif ou effet de niveau (stat `AugmentRerolls`). Un reroll remplace un choix.
- **Reroll doré** : un draft d'**augments** a **~5 % de chance** (`GoldRerollChance = 0.05`) de porter un reroll doré : **le premier reroll que le joueur utilise** dans ce choix d'augment donne un augment de **rareté +1**. Cela **n'existe que pour les augments**, jamais pour les enclumes.
- Un augment choisi est ajouté comme **passif** via `AugmentController` : aucun code spécial par augment.

**Quand tous les slots sont remplis**, après avoir choisi un augment de rareté R, le joueur choisit **un** des bénéfices suivants :

| Option | Règle |
|---|---|
| **Enclume** | une enclume à stats dont **les 3 choix sont de la rareté R** |
| **Remplacement** | le nouvel augment remplace un augment possédé selon R : **Argent** → un augment possédé de **rareté supérieure** ; **Or** → un augment possédé de **même rareté** ; **Prismatique** → un augment possédé de **rareté inférieure** |
| **Slot ou gold** | **+1 slot d'augment** si le joueur a moins de 6 slots, sinon du **gold selon la rareté** |

> **En place (phase 8)** :
> - `AugmentDefinition` = une rareté + un `PassiveDefinition` (nom, description, icône, effets). `AugmentCatalog` : 12 augments de test (4 Argent, 5 Or, 3 Prismatique : stats, `SpellCrit`, stacks par kill / par attaque, bonus par ennemi proche, sort à 3 éléments → buff, loot), slots 4 → 6, poids de rareté 55 / 33 / 12, **4 relances pour toute la run** (une par slot de base, chacune remplace un choix), relance dorée 5 %, gold par rareté si 6 slots.
> - `AugmentController` : draft à l'étape `Augment` (rareté tirée, 3 augments non possédés), relances, relance dorée (la première relance donne rareté + 1), slots pleins (enclume à stats de la rareté via `ShopService.OpenAnvil(forcedRarity)`, remplacement selon la règle ci-dessous, +1 slot puis gold). `AugmentPanel` remplace l'écran d'attente de la 7a ; `AugmentsView` affiche les slots au-dessus des items.
> - **Phase 9b** : +7 augments (19 en tout) : Volonté de fer (ténacité), Main guérisseuse (puissance des soins), Saignée (attaques qui saignent), Choc statique (1 coup de sort sur 6 étourdit), Barrière arcanique (bouclier après un sort à 2 éléments), Phénix (soin de 30 % PV max sous 30 %, 90 s), Brasier éternel (sorts qui brûlent).

Le cas **Argent** permet de **rétrograder** volontairement : un augment de rareté basse peut être très pertinent selon le build (par exemple pour un personnage avec beaucoup de PV), et le joueur doit pouvoir y accéder même quand ses slots sont remplis par des raretés supérieures.

### 9.5 Loot et sorts d'invocateur

- Les ennemis donnent le gold directement. Les **objets au sol** n'arrivent que par des **effets de passif ou d'augment** : `DropLootOnKill(chance, LootTable)`.
- `LootDrop` au sol, ramassé **au contact**. Types : **enclume à stats** (augment **Flambeur**) et **sort d'invocateur** (passif façon **Zoé**).
- **Sort d'invocateur ramassé** (`ConsumableSpell`, usage unique : Flash, Soin, Fantôme…) :
  - il va sur **D** si D est vide (le Flash reste sur F) ;
  - on **ne peut pas en ramasser un autre tant qu'on n'a pas utilisé celui-ci** ;
  - une fois utilisé, **D redevient vide**.

> **En place (phase 8)** : effet de passif `DropLoot` → `LootDropRequestedEvent` (passifs du joueur seulement) → `LootService` (scope Game) : sprites au sol face caméra, ramassés au contact (`LootSettings.PickupRadius`), disparus après 30 s ou en fin de vague. **Enclume à stats** → `BonusRewardEarnedEvent(FreeStatAnvil)`, ouverte gratuitement en fin de vague avant les récompenses prévues (`RewardController`). **Sort d'invocateur** → `SummonerSpellPickedEvent` : `AbilityCaster` le garde sur D pour un usage (depuis la refonte LoL ; avant : sur F), un seul à la fois (`SummonerSlotChangedEvent`). Sorts : **Fantôme** (+40 % vitesse 6 s) et **Embrasement** (sort ciblé, 150 + 40 % PM). Augments « Flambeur » (6 % par kill) et « Invocateur » (8 %).

### 9.6 Ennemis et boss

- Un ennemi est un **personnage comme les autres** : même prefab générique, `StatController`, `Health`, `BasicAttacker` (pas de dégâts de contact), passif optionnel. `EnemyDefinition` étend `CharacterDefinition` avec `XpReward`, `GoldReward` et le sort spécial. Les stats montent avec les vagues (`WaveScaling`).
- **Mana façon TFT** : l'ennemi gagne du mana **passivement** (`ManaPerSecond`), **à chaque attaque** (`ManaPerAttack`) et **en se faisant attaquer** (`ManaPerHitTaken`). À mana plein, il lance son `SpecialSpell` (un `SpellDefinition` Magic / Physical / Buff, lancé par `Spell.Cast`, sans éléments), puis le mana repart à `StartMana`.
- **IA « minimalement intelligente »** (`EnemyAI : ICommandSource`, mêmes ordres que le joueur, §4.2) : états `Chase → Attack → Cast` ; contournement d'obstacles ; ennemis à distance qui gardent leur portée ; respect des CC et de la ténacité (§5.7).
- **Centaines d'ennemis simultanés** :
  - **Hordes** : navigation par **flow field** (champ de vecteurs vers le joueur sur une grille, recalculé de façon étalée) + steering léger ; calcul en **Jobs/Burst** si besoin.
  - **Élites et boss** : `NavMeshAgent` (§4.4).
  - **Creep block** accepté : les ennemis se bloquent physiquement entre eux (layer `Enemy` × `Enemy` actif). Si la physique coûte trop cher, repli sur une séparation custom.
  - IA à fréquence réduite pour les ennemis éloignés, pooling, spawn hors écran.
- **Boss** (`BossDefinition : EnemyDefinition`) :
  - **Pattern plus complexe** : `PatternDefinition` (SO) en **phases** (seuils de PV), chaque phase = liste d'étapes `PatternStep` (lancer un sort, se déplacer, attendre, invoquer, télégraphier) en séquence ou tirage pondéré, exécutées par un `PatternRunner`.
  - **Jusqu'à 3 items équipés** comme TFT (`ItemDefinition[3]`) : stats et passifs via le même système d'items que le joueur.

> **En place (phase 9a)** :
> - `BossDefinition : EnemyDefinition` (`Title`, `Pattern`, `Items` ≤ 3) ; `PatternDefinition` = phases par seuil de PV (`HealthThreshold`, 1 pour la première), chacune en `Sequence` ou `WeightedRandom` ; `PatternStep` : `Wait` (l'IA habituelle pendant `Duration`), `CastSpell` (sort sur le joueur ou centré sur le boss avec `AtSelf`, télégraphié pendant son incantation), `Charge` (ligne télégraphiée puis ruée `CharacterMovement.Dash`, dégâts au contact), `Summon` (N ennemis autour du boss, via le `HordeSpawner`).
> - `PatternRunner` (prefab `Boss` = variant d'`Enemy`) : suspend `EnemyAI` pendant ses étapes, change de phase quand les PV passent sous un seuil (l'étape en cours se termine), applique les stats et passifs des items à l'`Init`, publie `BossSpawnedEvent`, `BossHealthEvent`, `BossDefeatedEvent`. `BossHudView` : barre de boss (nom, titre, PV, phase) sous le HUD de run.
> - **Vague de boss** : `WaveDefinition.Boss` + `BossPrefab` ; le boss apparaît au début de la vague, et la vague ne peut finir (timer **et** quota) qu'une fois le boss mort, sinon overtime. Les invocations comptent dans le quota.
> - **Grokk, roi des gobelins** (vague 5 du stage de test, timer 120 s) : 3000 PV, 2 items (Égide du feu solaire et Couperet noir depuis les objets de LoL). Phase 1 (100 %) : attente, coup de massue (zone de 2,6 sur le joueur), attente, charge. Phase 2 (≤ 60 %) : 4 gobelins, onde royale (zone de 4,5 autour de lui), charge, coup de massue. Phase 3 (≤ 30 %) : tirage pondéré, 5 gobelins. Valeurs provisoires.

> **En place (phase 6)** :
> - **Données** : `EnemyDefinition` = récompenses, `Navigation` (`FlowField` pour les hordes, `NavMesh` pour les élites) et mana TFT (`SpecialSpell`, `StartMana`, `ManaPerSecond`, `ManaPerAttack`, `ManaPerHitTaken` ; le max est la stat `MaxMana`). Ennemis de test (valeurs provisoires) : **Gobelin** (corps à corps, horde), **Archer squelette** (distance 6, horde), **Brute** (élite NavMesh, 600 PV, sort spécial **Fracas** : zone de 2,2 au sol sur la position du joueur, 0,9 s d'incantation **télégraphiée** par un cercle rouge, donc esquivable).
> - **IA** (`EnemyAI`, prefab `Enemy`) : Chase → Attack → Cast, via les mêmes composants que le joueur. Pas d'`Update` : `EnemyDirector` (scope Game) fait réfléchir chaque ennemi une fois par `ThinkInterval` (0,1 s), décisions étalées sur les frames. Hors de portée : les hordes suivent le **flow field** (`CharacterMovement.Steer`, `NavMeshAgent` désactivé), les élites leur chemin NavMesh (`RepathInterval`). À portée : `BasicAttacker` prend le relais (il ne poursuit plus lui-même : `ChasesTarget = false`), les ennemis à distance s'arrêtent à leur portée. Le windup n'est pas interrompu par l'IA.
> - **Flow field** (`FlowField`, C# pur testé ; `FlowFieldService`) : grille de `HordeSettings` (100 × 100, cellules de 1), murs lus une fois dans le NavMesh baké, Dijkstra 8 voisins sans couper les coins de mur, recalculé au plus toutes les 0,25 s et seulement si le joueur a changé de cellule.
> - **Creep block** : collisions physiques entre corps (layer par défaut) ; pas de séparation custom tant que le profiler ne le demande pas.
> - **Spawn** (`HordeSpawner`) : un ennemi toutes les `SpawnInterval` (2 s), tiré au poids dans `SpawnTable` (gobelin 6, archer 3, brute 1), sur un anneau de 16 à 22 autour du joueur (hors écran), sur le NavMesh et atteignable par le flow field, jusqu'à `MaxAlive` (18). Pooling (`PrefabPool`), retour au pool `DespawnDelay` après la mort. La mort du joueur vide la horde ; le joueur réapparaît au spawn après `GameFlowSettings.PlayerRespawnDelay` (3 s) — **provisoire** jusqu'aux vies de stage et au game over (phase 7).
> - **Debug** : le panneau de la scène de test affiche le nombre d'ennemis, arrête / relance le spawn et ajoute 50 ennemis d'un coup (`HordeDebugEvent`).
> - Perf mesurée : 200 ennemis ≈ 1,6 ms par frame dans l'éditeur (IA, physique, rendu).
> - **Phase 9b** : Fracas de la Brute ralentit (−40 %), le coup de massue de Grokk étourdit 0,75 s, son onde royale ralentit.
> - **Tirs ennemis esquivables** : `BasicAttackDefinition.Homing` (vrai par défaut, les auto-attaques du joueur ne ratent pas comme dans LoL) ; à faux (flèches de l'archer squelette), `Projectile.LaunchStraight` part en ligne droite vers la position de la cible au moment du tir, touche le premier ennemi du tireur sur sa route et disparaît après la portée + 1,5.
> - **Phase 9c, nouveaux ennemis** (`EnemyContentSetup`, valeurs provisoires, ajoutés aux vagues du stage de test à partir de la vague 2) :
>   - **Loup** (horde, 60 PV, très rapide) : passif d'ennemi « Morsure », ses attaques font saigner (mêmes briques de passif que le joueur).
>   - **Mage squelette** (horde, distance 7, tir magique droit) : sort spécial **Trait de givre**, skillshot télégraphié par une ligne rouge pendant son incantation (0,7 s), ralentit de 40 %.
>   - **Chaman gobelin** (horde, soutien) : sort spécial **Protection tribale**, bouclier de 60 et +30 % vitesse d'attaque / +15 % vitesse pour lui et ses alliés à moins de 5 m (`SpellDefinition.AffectsAllies`), télégraphe **vert**.
>   - **Golem de pierre** (élite NavMesh, 900 PV, 60 armure, 50 % ténacité) : sort spécial **Séisme**, zone de 3,5 près de lui qui immobilise 1,2 s.
>   - `SpellExecutor.Telegraph` : les skillshots ennemis sont télégraphiés par une ligne ; rouge pour ce qui fait mal, vert pour un soutien.
> - *Reste : `WaveScaling` et vagues (phase 7), IA à fréquence réduite pour les ennemis lointains, boss (`PatternRunner`).*

### 9.7 Méta-progression (rogue-lite)

- **Monnaie persistante** : gagnée **à la fin d'une partie si le stage est clear** (full clear, vagues bonus comprises).
- Elle est dépensée dans des **arbres de compétences accessibles depuis le menu**, composés de nœuds (`MetaNode` : id, prérequis, coût, niveaux, effets). Deux familles :

| Arbre | Rôle |
|---|---|
| **Par personnage** (`CharacterMetaTree`) | améliore les **sorts** du champion, ses **stats**, le **gold de départ**, la possibilité d'avoir **un augment dès le début**, le **passif**, etc. (l'ancien arbre par élément disparaît avec les éléments) |

- **Effets génériques** (`MetaEffect`), jamais limités à cette liste : bonus de stat, bonus de dégâts d'un sort, gold de départ, augment de départ (avec une rareté), amélioration d'un sort, amélioration du passif… Ils sont appliqués au **début de la run** (`RunModifiers`, `StatModifier` avec Source = le nœud méta) ou lus par les systèmes concernés.
- Le **nombre de nœuds et de niveaux** est une donnée à définir plus tard.
- Autres déblocages possibles : items, augments, personnages.
- **`MetaSave`** (JSON) : monnaie et nœuds achetés.
- **`RunSnapshot`** : état complet de la run (niveau, XP, rangs des sorts, stacks de passifs, items, augments, gold, vies, vague en cours) sérialisé en JSON, pris **au début de chaque vague**. Quitter une vague puis reprendre recharge ce snapshot. Tout état de jeu doit donc être sérialisable.
- **Solo uniquement**, un seul personnage modulable au prototype.

### 9.8 Technique & perf
- **Object Pooling** : `UnityEngine.Pool.ObjectPool<T>` (intégré), via `PrefabPool` (`Systems.Pooling`, service du `GameLifetimeScope` : un pool par prefab, instances créées par le conteneur). Utilisé par les projectiles et les VFX de sorts.
- **Caméra** : `CameraController` maison, verrouillée / libre façon LoL (§4.8). Cinemachine reste une option si le besoin grossit.
- **Détection de cibles** : `Physics.OverlapSphereNonAlloc` ou grille spatiale ; éviter `FindObjectsOfType` ; mettre en cache.
- **Pas de `GetComponent` en boucle** : références mises en cache à l'`Init`.
- **Éviter l'allocation** : listes réutilisées, `struct` pour `DamageInfo`.
- **Animations** : privilégier flipbook/sprite swap simple pour les ennemis en masse plutôt qu'un Animator par entité.
- **UI** : HUD (PV, XP, timer de vague, quota, vies, icônes de sorts), écrans de boutique / enclumes / augments, damage numbers (TextMeshPro poolé ou rendu par particules).
- **Audio** : pool d'`AudioSource` + limitation du nombre de sons simultanés.
- **Sauvegarde** : JSON simple (`JsonUtility`) pour la méta-progression.

---

## 10. Arborescence du projet

> L'arborescence **réelle** (Systems / Features / Containers) est décrite au **§14.6** et fait foi. Ci-dessous, le découpage **par module** à suivre à l'intérieur de `Assets/Features/` au fil des phases.

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/            (Character, CharacterLevel, Faction, GameManager, Pooling)
│   │   ├── Stats/           (Stat, StatModifier, StatController, StatBlock, StatusEffect)
│   │   ├── Combat/          (Health, DamageInfo, DamageReceiver, DamageCalculator)
│   │   ├── Movement/        (CharacterMovement)
│   │   ├── Commands/        (CharacterCommand, CommandController, ICommandSource, BasicAttacker)
│   │   ├── Spells/          (SpellDefinition, AbilityCaster, AbilityRanks, SpellCaster, SpellExecutor, CastContext…)
│   │   ├── Input/           (PlayerCommandInput, SpellIndicator, ClickMarker)
│   │   ├── AI/              (EnemyAI, EnemyDefinition, BossDefinition, PatternRunner, FlowField)
│   │   ├── Progression/     (ExperienceCurve, StatGrowth)
│   │   ├── Meta/            (MetaNode, MetaEffect, CharacterMetaTree, MetaSave, RunModifiers)
│   │   ├── Items/           (ItemDefinition, AnvilDefinition, StatChoiceDefinition, ElixirDefinition, Inventory, Wallet, Shop)
│   │   ├── Loot/            (LootDrop, LootTable, ConsumableSpell)
│   │   ├── Augments/        (AugmentDefinition, AugmentController, AugmentDraft)
│   │   ├── Passives/        (PassiveDefinition, Passive, StackCounter, CharacterEvents, CapabilityController)
│   │   ├── Run/             (RunDirector, WaveDefinition, EnemySpawner, Intermission)
│   │   └── UI/
│   ├── Data/                (Characters, Enemies, Spells, Passives, Items, Augments, Waves, XPCurve)
│   ├── Prefabs/
│   ├── Art/
│   └── Scenes/
```
Utiliser des **Assembly Definitions** (`.asmdef`) par dossier majeur pour accélérer la compilation.

---

## 11. Plan de réalisation (ordre conseillé)

| Phase | Contenu | Résultat jouable |
|---|---|---|
| **0** ✅ | **Fondations** (§14) : dépôt Git, arborescence et asmdef, `IEventBus`, `GameState` (+ `Win`), `RootLifetimeScope`, SO de réglages, tests EditMode + PlayMode. `RunStateMachine` ajoutée avec la phase 7a. | Projet propre qui compile, états de jeu et événements en place |
| **1** ✅ | `CharacterMovement` (clic droit, **NavMesh**), marqueur, caméra verrouillée / libre (§4.8), `TestScene`, prefab 2.5D (`Character` + variant `Player`, sprite billboard, §3) | Un perso qui se déplace au clic en évitant les obstacles |
| **2** ✅ | `Stat` / `StatModifier` / `StatController` + toutes les stats + tests unitaires | Stats modifiables |
| **3** ✅ | `CharacterDefinition`, `Character.Init`, `BasicAttackDefinition`, `CharacterLevel` (XP, points de compétence, croissance), vitesse lue depuis `MoveSpeed`. Sprite et taille du corps depuis la définition. | Un perso configurable par données |
| **4** ✅ | `Health`, `DamageInfo`, formules, régénération des PV, barres de PV, chiffres de dégâts ; boucliers, CC et ténacité (avec la 9b) | Dégâts corrects |
| **5** 🟡 | Clic droit sur ennemi, `BasicAttacker`, projectiles poolés, Q + clic gauche (attack-move) ✅, dummies d'entraînement (XP au kill). *Reste : `CommandController`.* | Attaque de base façon LoL |
| **5b** ✅ (remplacée par la 10) | `ManaPool`, `ElementSelection` (A/Z/E, T reset), `SpellBook` + `SpellResolver`, coût en mana + UI, déblocage d'éléments par points (niv. 1/3/5, Ctrl + A/Z/E) | Éléments activables, coût affiché |
| **5c** ✅ (remplacée par la 10) | R + visée + indicateurs, sorts Magic/Physical/Buff, niveaux d'éléments, cooldown par lancement, compétence propre F (Flash) | Sorts lancés à la souris |
| **6** ✅ | `EnemyDefinition`, `EnemyAI` (flow field + NavMesh pour élites), mana TFT + sort spécial, spawner, XP/gold au kill | Un combat complet avec des hordes |
| **7** ✅ | `RunDirector` (timer + quota, fin de vague = timer ET quota, vies), `Wallet`, `WaveRewardSchedule`, victoire / défaite (7a) ; `Inventory` (6 slots), boutiques, enclumes, élixirs, items (7b) | Boucle de run + économie |
| **8** ✅ | `CharacterEvents`, passifs (briques génériques : stats, triggers, stacks, ennemis proches), capacités (`SpellCrit`), augments (4 → 6 slots, relances, relance dorée, slots pleins), passifs d'items, loot (enclume à stats, sorts d'invocateur) | Builds variés |
| **9** 🟡 | Boss à patterns et items ✅ (9a). Statuts (stun, root, silence, ralentissement, saignement, brûlure, ténacité), soins et boucliers, statuts sur les sorts, +14 items, +7 augments ✅ (9b). 4 nouveaux ennemis (loup, mage squelette, chaman gobelin, golem de pierre) ✅ (9c). *Reste : nouveaux éléments et sorts (avec un deuxième personnage), équilibrage.* | Prototype riche |
| **10** 🟡 | **Refonte LoL** : sorts par champion (Q W E R à rangs, D F), Ezreal, touches par position, dézoom de la caméra ✅. Objets de LoL (65, passif propre, force adaptative sur les prismatiques, groupes uniques) ✅. *Reste : équilibrage, UI façon LoL (stats et augments au même endroit, boutique : items seuls, détail au clic).* | Ezreal jouable |
| **11** | Méta-progression (arbres par personnage, monnaie persistante), `RunSnapshot` (quitter/reprendre), polish, UI, audio | Prototype solide |

---

## 12. Risques & pièges à anticiper

- **Un SO mutable partagé** : ne jamais écrire de l'état runtime dans un ScriptableObject.
- **Stats modifiées directement** au lieu de passer par des modificateurs → impossible à annuler/déboguer.
- **Ordre d'exécution** : initialiser stats **avant** mana, sorts et PV (`Character.Init` orchestre l'ordre).
- **Perf avec des centaines d'ennemis** : profiler tôt (Profiler + Frame Debugger), éviter `Update` sur chaque ennemi si possible (manager central).
- **Pause des menus** (boutique, augments) : utiliser `Time.unscaledDeltaTime` pour l'UI.
- **Rigidbody + beaucoup d'ennemis** : envisager un déplacement cinématique ou une séparation custom si la physique coûte trop.
- **Valeurs d'équilibrage en dur** : tout dans des SO pour itérer sans recompiler.
- **19 sorts par personnage** : prévoir des outils d'édition (validation des recettes manquantes, tableau récapitulatif, duplication de sorts) pour ne pas se noyer.
- **Centaines d'ennemis** : pas de `NavMeshAgent` pour les hordes (flow field), physique des ennemis à profiler tôt (repli : séparation custom).
- **Passifs, items et augments** : tous passent par `PassiveController` ; garder un `Source` unique par effet pour retirer/rafraîchir proprement.
- **Rogue-lite** : tout état de run doit être remis à zéro proprement entre deux runs (stacks, modificateurs, inventaire).
- **Quitter/reprendre** : le `RunSnapshot` exige que stacks, modificateurs, items, augments et rangs des sorts soient sérialisables ; prévoir ce besoin dès les phases 2 et 3.

---

## 13. Questions ouvertes (à valider avant de coder)

**Résolu** : plan XZ ; reset (T) ; une recette = un sort (19 par perso) ; cooldown par lancement {2, 3.5, 5.5} s ; compétence propre F (Flash 20 s) remplaçable par un sort d'invocateur ; points de compétence aux niveaux 1 à 15 pour monter le niveau des éléments (une fois par sort) ; niveaux 16 à 18 : stats seules, sauf surcharge débloquée en méta ; déblocage des éléments aux niveaux 1 / 3 / 5 ; 3 stacks max par sort ; sorts Magic / Physical / Buff ; passifs à persistance et sources de stacks variables ; XP et gold directs au kill ; fin de vague = timer ET quota ; 3 vies de stage, game over = rejouer le stage depuis la vague 1 ; ordre des fins de vague (augment, enclume prismatique gratuite à 3 choix avec 1 reroll par choix, puis boucle boutique légère / augment / boutique complète) ; vagues bonus : boutique complète après chaque vague et augment toutes les deux vagues ; un élixir max, enclumes à items comptées dans la limite d'un item ; enclumes à stats : une rareté tirée, 3 choix de même rareté ; augments toutes les 3 vagues, reroll doré ~5 % ; méta par arbres d'éléments et de personnages ; quitter/reprendre au début de la vague ; centaines d'ennemis avec creep block ; mana TFT ; boss à patterns avec 3 items ; **pas de crit sur les sorts sauf passif/augment** ; solo, un seul perso de test.

**Validé (13/10/2026, refonte LoL)** : abandon des éléments ; chaque champion a ses sorts Q W E R (rangs LoL, 1 point par niveau jusqu'au 18), un passif, Flash sur F et un emplacement D pour les sorts d'invocateur ramassés ; touches par position ; personnage d'essai Ezreal. Les décisions « Résolu » ci-dessus qui parlent d'éléments, de recettes, de reset (T), de surcharge ou de compétence propre F sont **caduques**.

**Validé (phase 8)** : 4 slots d'augments de base, 6 maximum avec les augmentations ; 4 relances pour toute la run (une par slot de base). **Validé** : la **mort du joueur** est un **game over immédiat** (les vies de stage ne se perdent qu'au timer).

Il ne reste **aucune question bloquante** pour la phase 1. Les valeurs d'équilibrage (prix, durées, poids de rareté, nombre de vagues, niveaux méta) sont des données à régler plus tard dans les ScriptableObjects.

---

## 14. Architecture technique et conventions (obligatoires)

### 14.1 Les cinq règles

| # | Règle | Conséquence |
|---|---|---|
| 1 | **State machine obligatoire** pour les états du jeu (Menu, Game, Pause, Win, GameOver, et d'autres si nécessaire) | Aucun `bool isPaused` ni `if (state == …)` éparpillé ; tout passe par `TryChange` |
| 2 | **EventBus** entre les systèmes | Pas de référence directe entre systèmes ; on publie et on s'abonne à des événements typés |
| 3 | **Injection de dépendances avec VContainer** | Ni singleton statique, ni `FindObjectOfType`, ni service locator |
| 4 | **Conception orientée données** : ScriptableObjects | Persos, ennemis, sorts, vagues, stages, options… sont des assets ; le code est générique |
| 5 | **Projet rangé et versionné avec Git** | Arborescence claire, asmdef, `.gitignore` / `.gitattributes`, conventions de branches et de commits |

### 14.2 State machines

On **réutilise `GameStateMachine<TId>`** (namespace `Systems.StateMachine`, `TId : struct, Enum`) : générique, avec règles de transition sérialisées dans l'Inspector. Unity ne sérialise pas un générique ouvert, donc **une classe concrète par enum** (`GameFlowStateMachine : GameStateMachine<GameState>`, `RunStateMachine : GameStateMachine<RunState>`…).

API : `TryChange(TId)` (renvoie `false` et **logue** si une règle refuse), `CanChange`, `CurrentState` / `HasState`, `event Changed(TId? from, TId to)` (`from` est `null` pour le tout premier état). Une demande faite **depuis** un handler de `Changed` est mise en file et appliquée juste après. Un état sans règle peut aller partout ; le premier état est toujours accepté.

> ⚠️ Les règles sont sérialisées en **entiers** : on n'ajoute de nouveaux états **qu'à la fin** de l'enum (insérer `Loading`/`Win` au milieu avait décalé toutes les règles de la scène Menu et cassé le bouton Play).

**Machine globale `GameFlowStateMachine`** :

```csharp
public enum GameState { Menu, Loading, Game, Pause, Win, GameOver }   // Loading : si le chargement de scène est asynchrone
```

Instance unique sur le **prefab `RootLifetimeScope`** (règles éditées là, ou régénérées par *Tools > Project Setup*).

| De | Vers autorisés |
|---|---|
| Menu | Loading |
| Loading | Game, Menu |
| Game | Pause, Win, GameOver |
| Pause | Game, Menu |
| Win | Loading (rejouer ou stage suivant), Menu |
| GameOver | Loading (rejouer le stage depuis la vague 1), Menu |

**Flux de scènes** (`GameFlowController`, root) :
- `PlayRequestedEvent` → `Loading` → chargement **Single** de `GameFlowSettings.GameScene` (aujourd'hui `TestScene`).
- Chaque scène déclare l'état qu'elle représente via un **`SceneEntryPoint`** enregistré par son scope (`MenuLifetimeScope` → `Menu`, `GameLifetimeScope` → `Game`). Ainsi **n'importe quelle scène peut être lancée directement** depuis l'éditeur et passe quand même par la state machine.
- Retour à `Menu` (depuis `Pause`, `Win`, `GameOver`) → chargement Single de `GameFlowSettings.MenuScene`.
- `RunEndedEvent` (`RunDirector`) → `Win` ou `GameOver` ; « Rejouer » publie `PlayRequestedEvent` (→ `Loading` → la scène de jeu est rechargée).
- `QuitRequestedEvent` (bouton Quitter du menu, bouton Quit de debug, menu pause) → `Application.Quit` (arrêt du Play mode dans l'éditeur).
- **Pause** : Échap en jeu (`CancelEvent`) annule d'abord la visée en cours ; sans visée, `PauseInput` (scope Game) publie `PauseRequestedEvent` → `Pause` (`TimeScaleService` met le temps à 0, `InputMapSwitcher` passe sur la map UI). `PauseMenu` (UIDocument au-dessus du HUD) : Reprendre (`ResumeRequestedEvent` → `Game`), Options, Menu principal (`MainMenuRequestedEvent` → `Menu`), Quitter. Échap dans la pause (`UiCancelEvent`, map UI) : retour depuis les options, sinon reprise.

**Machine de run `RunStateMachine`** (active pendant `Game`, pilotée par `RunDirector`, §9.1) :

```csharp
public enum RunState { PreWave, Combat, Overtime, Reward }
```

| De | Vers | Déclencheur |
|---|---|---|
| PreWave | Combat | compte à rebours de 10 s terminé |
| Combat | Reward | timer écoulé **et** quota atteint |
| Combat | Overtime | timer écoulé, quota non atteint (−1 vie, puis −1 vie / 30 s) |
| Overtime | Reward | quota atteint |
| Reward | PreWave | écran de fin de vague fermé |

Le type d'écran de `Reward` (augment, enclume gratuite, boutique légère, boutique complète) est une **donnée** (`WaveRewardSchedule`), pas un état. Victoire (dernière vague bonus terminée) et défaite (0 vie) sont **publiées en événements** ; `GameFlowController` les transforme en `GameState.Win` / `GameState.GameOver`.

**Autres machines** :
- **Lanceur du joueur** : `Idle → Targeting → Casting` (§7.4).
- **Ordres du perso** : `CommandController` (§4.2) ; l'ordre courant est l'état.
- **Ennemis en masse** : **pas** de `MonoBehaviour` par machine. Une FSM légère (enum + `switch`, ou struct) dans l'IA, sinon on paie des centaines d'objets.

**Règles d'usage**
- Les panneaux et la pause réagissent aux **changements d'état** (`GameStateChangedEvent`) : `MainMenuPanel` s'ouvre en `Menu` ; `GameHUD` pilote les autres panneaux via un dictionnaire état → panneau (data-driven).
- `Time.timeScale` est géré **à un seul endroit** (`TimeScaleService`, abonné aux changements d'état : `Pause`, `Win`, `GameOver` et `RunState.Reward` → 0, sinon 1), jamais depuis l'UI.
- L'action map active suit l'état (`InputMapSwitcher`) : `Gameplay` en `Game` (hors écrans de fin de vague), `UI` sinon.
- Une transition refusée par les règles doit être **loguée** (voir §14.8).

### 14.3 EventBus

**Principe** : un système ne connaît pas ceux qui l'écoutent. Il publie un **événement typé** (`readonly struct`), les autres s'abonnent.

Deux niveaux, à ne pas confondre :

| Niveau | Usage | Mécanisme |
|---|---|---|
| **Bus global** | événements **grossiers** entre systèmes (vagues, kills, UI, audio, run, input) | `IEventBus` injecté |
| **`CharacterEvents`** (§8.1) | événements **par entité** pour passifs, items, augments (`BasicAttackHit`, `DamageTaken`…) | `event` C# sur chaque personnage, **hors bus global** (des centaines d'ennemis) |

**Squelette** (namespace `Systems.EventBusses`, injecté et non statique) :

```csharp
public interface IEvent { }

public interface IEventBus {
    void Publish<T>(in T evt) where T : struct, IEvent;
    IDisposable Subscribe<T>(Action<T> handler) where T : struct, IEvent;
}

public sealed class EventBus : IEventBus {
    private readonly Dictionary<Type, Delegate> _handlers = new();

    public void Publish<T>(in T evt) where T : struct, IEvent {
        if (_handlers.TryGetValue(typeof(T), out var d)) ((Action<T>)d)?.Invoke(evt);   // pas de boxing
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IEvent {
        _handlers[typeof(T)] = _handlers.TryGetValue(typeof(T), out var d) ? Delegate.Combine(d, handler) : handler;
        return new Subscription(() => Unsubscribe(handler));
    }
    // Unsubscribe<T> retire le handler ; Subscription implémente IDisposable
}
```

**Conventions**
- Nom : `XxxEvent`, `readonly struct`, rangé dans `Features/<Domaine>/Events`.
- **Abonnement symétrique** : on garde les `IDisposable` retournés (`DisposableBag` / liste) et on les libère dans `OnDestroy` / `Dispose`.
- Handlers **courts et sans allocation** ; pas de logique lourde dans un handler.
- Le bus ne remplace pas les appels directs **à l'intérieur d'un même prefab** (les composants d'un `Character` peuvent se référencer entre eux).
- **Aucun bus statique** : l'ancien `InputsEventBus` (et le déplacement WASD qui l'utilisait) a été supprimé. `IEventBus` est le seul bus.
- Les abonnements sont regroupés dans un `DisposableBag` (`Systems.EventBusses`) quand un composant en a plusieurs.

**Catalogue initial d'événements**

| Événement | Publié par | Écouté par |
|---|---|---|
| `GameStateChangedEvent` (`From` nullable, `To`) | `GameFlowController` | `MainMenuPanel`, `GameHUD`, `TimeScaleService`, `InputMapSwitcher`, audio |
| `PlayRequestedEvent`, `QuitRequestedEvent` | `MainMenuPanel`, `QuitOverlay` | `GameFlowController` |
| `PointerPositionEvent`, `PointerCommandEvent` (position **écran**), `ConfirmEvent`, `CancelEvent`, `AttackMoveEvent`, `AbilityKeyEvent` (slot Q W E R D F, `LevelUp` si Ctrl, `Released`), `StopEvent` | `InputReader` | `PlayerCommandInput`, `CameraController`, `QuitOverlay`, `AbilityCaster` |
| `CameraLockEvent`, `ZoomEvent` | `InputReader` | `CameraController` |
| `WaveStartedEvent`, `WaveEndedEvent`, `LifeLostEvent` | `RunDirector` | HUD, audio, `WaveSpawner` |
| `StageClearedEvent`, `StageFailedEvent` | `RunDirector` | `GameFlowController`, méta-progression |
| `EnemyKilledEvent` (xp, gold, position) | `Character` ennemi à sa mort | `Character` joueur (XP) ; plus tard `Wallet`, loot |
| `DamageDealtEvent` (position, montant, type, crit, joueur touché ?) | `Character` touché | `DamageNumbersView` ; plus tard audio, VFX |
| `PlayerHealthChangedEvent`, `PlayerDiedEvent` | `Character` joueur | `PlayerHud` ; plus tard `GameFlowController` (GameOver) |
| `PlayerSpawnedEvent` (transform du joueur) | `PlayerSpawner` | `CameraController` |
| `XpGrantedEvent` | `CharacterDebugPanel` (plus tard : kills) | `Character` joueur |
| `XpChangedEvent`, `LevelUpEvent`, `StatChangedEvent` | `Character` joueur | `PlayerHud`, `CharacterDebugPanel` |
| `AbilitiesChangedEvent` (sorts, rangs, « + » possibles, points) | `AbilityCaster` | `PlayerHud` |
| `AbilityRankUpRequestedEvent` | bouton « + » du HUD | `AbilityCaster` |
| `GameplayFeedbackEvent` (message) | `AbilityCaster`, boutiques… | `PlayerHud` |
| `PlayerManaChangedEvent` | `Character` joueur | `PlayerHud` |
| `SpellCastEvent` (sort, lancé par le joueur ?) | `SpellExecutor` | plus tard : passifs, audio |
| `AimModeEvent` (None / AttackMove / Spell / Signature) | `PlayerCommandInput`, `SpellTargeting` | les mêmes (une seule visée à la fois) |
| `CooldownStartedEvent` (slot, durée) | `AbilityCaster` | `PlayerHud` |
| `GoldChangedEvent`, `ItemPurchasedEvent`, `AugmentChosenEvent` | `Wallet`, `Shop`, `AugmentController` | HUD, `PassiveController` |

### 14.4 Injection de dépendances (VContainer)

**Hiérarchie de scopes**

| Scope | Durée de vie | Contenu |
|---|---|---|
| `RootLifetimeScope` | toute l'application (prefab `Assets/Prefabs/RootLifetimeScope.prefab`, référencé par `Assets/Data/Settings/VContainerSettings.asset`, lui-même dans les *Preloaded Assets*) | **En place** : `IEventBus`, `IPanelManager`, `InputReader`, `GameFlowStateMachine`, `GameFlowController`, `InputMapSwitcher`, `TimeScaleService`, `CursorConfinement`, `UserOptions`, SO `GameFlowSettings`, `CameraSettings` et `ExperienceCurve`. **Plus tard** : `SaveService` / `MetaSave`, catalogues |
| `MenuLifetimeScope` | scène `Menu` | `MainMenuPanel`, `OptionsPanel` (UIDocument « OptionsPanel », au-dessus du menu), `SceneEntryPoint(Menu)` |
| `GameLifetimeScope` | **un stage** (aujourd'hui `TestScene`) | **Root** : `ControlsSettings` (§4.7) et `SpellSettings` (§7.2) en plus. **Game, en place** : la `Camera` principale, `PrefabPool`, `ProjectileLauncher`, `SpellExecutor`, `CameraController`, `QuitOverlay`, `CharacterDebugPanel`, `PlayerHud` et `DamageNumbersView` (UIDocument « GameHud »), `PauseMenu` (UIDocument « PauseMenu ») et `PauseInput`, `PlayerSpawner` (prefab `Player`, `CharacterDefinition`, point de spawn ; respawn provisoire), `EnemySpawner` (liste `CharacterSpawn` : prefab + définition + point, aujourd'hui les 3 dummies), `HordeSettings`, `FlowFieldService`, `EnemyDirector`, `HordeSpawner`, `Wallet`, `StageDefinition`, `RunStateMachine`, `RunDirector`, `RunHudView` (sur « GameHud »), `RunEndPanel` (UIDocument), `RewardController`, `ShopCatalog`, `PlayerUpgrades`, `Inventory`, `ShopService`, `ShopPanel` et `AnvilPanel` (UIDocuments), `InventoryView` (sur « GameHud »), `AugmentCatalog`, `AugmentController`, `AugmentPanel` (UIDocument), `AugmentsView` (sur « GameHud »), `LootSettings`, `LootService`, `BossHudView` (sur « GameHud »), `SceneEntryPoint(Game)`. Le joueur spawné est injecté par le conteneur (`PlayerCommandInput`, `CharacterVisual`…). **Plus tard** : `RunSnapshotService`, le joueur |

VContainer crée le root **une fois**, avant le premier scope de scène, et le garde entre les chargements (`DontDestroyOnLoad`) ; un scope de scène sans `parentReference` en devient automatiquement l'enfant. Les services du root **ne doivent jamais être ré-enregistrés** dans un scope de scène (sinon deux `EventBus` coexistent et les événements se perdent).

Un **`GameLifetimeScope` par stage** donne gratuitement la **remise à zéro** de tout l'état de run (gold, items, stacks, vies) : détruire le scope = nouvelle run propre (§12).

**Conventions**
- **Classes C# pures** : injection par **constructeur** ; les systèmes sans `MonoBehaviour` implémentent `IInitializable`, `ITickable`, `IFixedTickable`, `IDisposable` et se déclarent par `RegisterEntryPoint` (ex. `RunDirector` n'a pas besoin d'être un `MonoBehaviour`).
- **`MonoBehaviour`** : injection par **méthode** `[Inject] void Construct(...)` plutôt que par champ.
- **Instanciation** : tout prefab créé à l'exécution (joueur, ennemis, projectiles, loot) passe par `IObjectResolver.Instantiate` pour que ses composants soient injectés ; les **pools** utilisent cette même fabrique. Pour les centaines d'ennemis, on injecte **une fois** à la création puis on appelle `Init(EnemyDefinition)` à chaque réutilisation.
- Dépendre d'**interfaces** (`IEventBus`, `IWallet`…) quand on veut tester ou remplacer.
- **Abonnements au bus** : dans `Initialize()` (`IInitializable`) pour les classes C#, dans `Construct` ou `Start` pour les `MonoBehaviour` ; jamais avant l'injection.
- **Interdits** : `static` mutable (y compris les « managers » statiques), singletons, `FindObjectOfType`, `Camera.main`, `Resources.Load` pour la logique de jeu. Seuls les outils éditeur (`Assets/Editor`) peuvent chercher des objets dans la scène.

### 14.5 Conception orientée données (ScriptableObjects)

**Règles**
1. **Tout le contenu et tous les réglages sont des assets** : le code ne contient aucune valeur d'équilibrage.
2. Un SO est **immuable à l'exécution** : l'état vit dans des instances runtime (déjà le cas pour `Spell`, `Passive`, `StackCounter`).
3. Les SO sont **injectés** (`RegisterInstance` de catalogues), jamais cherchés par nom.
   - En place : `GameFlowSettings` (scènes Menu / Game) et `CameraSettings` dans `Assets/Data/Settings`, enregistrés par le `RootLifetimeScope`.
   - Personnages : `Char_<Nom>`, `StatBlock_<Nom>`, `StatGrowth_<Nom>`, `BasicAttack_<Nom>` dans `Assets/Data/Characters/<Nom>/` ; `ExperienceCurve` dans `Assets/Data/Progression/`. *Tools > Project Setup* crée ces assets **une seule fois** (valeurs provisoires) et ne les écrase jamais ensuite.
4. Chaque SO a un `OnValidate` et un menu éditeur « Valider les données » (recettes manquantes, références nulles, doublons).

**Catalogue des ScriptableObjects**

| Domaine | SO |
|---|---|
| Personnages | `CharacterDefinition`, `StatBlock`, `StatGrowth`, `BasicAttackDefinition`, `PassiveDefinition` |
| Sorts | `SpellDefinition` (rangs, §7.2), `SpellSettings` (blink, prefabs d'effets) |
| Ennemis | `EnemyDefinition`, `BossDefinition`, `PatternDefinition` |
| Run | **`StageDefinition`** (liste de vagues, `WaveRewardSchedule`, `BonusWaveCount`, `StageLives`, difficulté), `WaveDefinition`, `WaveRewardSchedule`, `ExperienceCurve` |
| Économie | `ItemDefinition`, `AnvilDefinition`, `StatChoiceDefinition`, `ElixirDefinition`, `AugmentDefinition`, `LootTable` |
| Méta | `MetaNode`, `CharacterMetaTree` |
| Options | **`GameOptions`** (valeurs par défaut : volumes, qualité, sensibilité, bindings par défaut) ; les choix du joueur (`UserOptions`) sont **sauvegardés en JSON** par-dessus |
| Catalogues | `CharacterCatalog`, `ItemCatalog`, `AugmentCatalog`, `StageCatalog` |

**Nommage** : `Char_<Nom>`, `Enemy_<Nom>`, `Spell_<Champion>_<Touche>` (ex. `Spell_Ezreal_Q`), `Item_<Nom>`, `Aug_<Nom>`, `Stage_<Nom>`, `Wave_<Stage>_<N>`.

### 14.6 Arborescence et assemblies

On sépare **`Systems`** (infrastructure réutilisable, sans rien de spécifique au jeu) et **`Features`** (le jeu). Les scopes VContainer, qui assemblent les deux, vivent dans leur propre assembly. Arborescence **réelle** :

```
Assets/
├── Systems/                 Game.Systems — infrastructure, ne connaît pas le jeu
│   ├── StateMachine/        GameStateMachine<TId>
│   ├── EventBusses/         IEvent, IEventBus, EventBus, DisposableBag
│   ├── Pooling/             PrefabPool
│   ├── Inputs/              InputReader, Events/ (événements d'input)
│   └── UIManagement/        IPanelManager, PanelManager, OpenClosePanel
├── Features/                Game.Features — le jeu
│   ├── Flow/                GameFlowController, GameFlowSettings (SO), SceneEntryPoint,
│   │                        InputMapSwitcher, TimeScaleService, CursorConfinement, PauseInput
│   ├── Options/             UserOptions (choix du joueur, JSON)
│   ├── StateMachines/       GameState, GameFlowStateMachine
│   ├── Events/              GameStateChangedEvent, requêtes de flux (Play/Pause/Resume/MainMenu/Quit, Options), StatChangedEvent
│   ├── CameraControl/       CameraController, CameraSettings (SO), Billboard
│   ├── Characters/          Character, CharacterEvents, CharacterDefinition / EnemyDefinition (SO), CharacterVisual, Faction,
│   │                        PlayerSpawner, EnemySpawner, RespawnOnDeath
│   ├── Progression/         CharacterLevel, StatGrowth (SO), ExperienceCurve (SO)
│   ├── Combat/              BasicAttacker, BasicAttackDefinition (SO), DamageInfo, DamageCalculator, Health, ManaPool, DamageType,
│   │                        Projectile, SkillshotProjectile, ProjectileLauncher, TargetPicker, ITargetSelector
│   ├── Spells/              SpellDefinition, SpellSettings (SO), AbilityRanks, AbilityCaster, SpellMath,
│   │                        SpellCaster, SpellExecutor, SpellTargeting, SpellIndicator, SpellVfx
│   ├── Movement/            CharacterMovement
│   ├── Enemies/             EnemyAI, EnemyDirector, FlowField, FlowFieldService, HordeSpawner, HordeSettings (SO),
│   │                        BossDefinition, PatternDefinition (SO), PatternRunner
│   ├── Economy/             Wallet, Inventory, PlayerUpgrades, ShopService, StatBonus, ItemDefinition, ElixirDefinition,
│   │                        StatChoiceDefinition, AnvilDefinition, ShopCatalog (SO)
│   ├── Passives/            PassiveDefinition (SO), Passive, PassiveController, CapabilityController
│   ├── Augments/            AugmentDefinition, AugmentCatalog (SO), AugmentController
│   ├── Loot/                LootService, LootSettings (SO)
│   ├── Run/                 RunDirector, RunStateMachine, RewardController, StageDefinition, WaveDefinition, WaveRewardSchedule (SO)
│   ├── Controls/            PlayerCommandInput, ClickMarker, RangeIndicator
│   ├── Stats/               Stat, StatModifier, StatController, StatBlock (SO), TimedModifiers…
│   └── UI/                  PlayerHud, AbilityBarView, UiStyles, DamageNumbersView, HealthBarView, GameHUD, QuitOverlay,
│                            Panels/ (MainMenuPanel, PauseMenu, OptionsPanel, OptionsView, RunEndPanel, ShopPanel, AnvilPanel, AugmentPanel, OfferCard),
│                            RunHudView, InventoryView, AugmentsView, BossHudView, DebugTools/CharacterDebugPanel
├── Containers/              Game.Containers — RootLifetimeScope, MenuLifetimeScope, GameLifetimeScope
├── Editor/                  Game.Editor — ProjectSetup, CharacterPrefabSetup, EnemySetup, RunSetup, EconomySetup, PassiveSetup, BossSetup, TestSceneSetup, BuildScript
├── Data/                    Settings/ (GameFlowSettings, CameraSettings, ControlsSettings, SpellSettings, HordeSettings, VContainerSettings),
│                            Spells/Ezreal/ (Spell_Ezreal_Q…R), Spells/Signature/Spell_Flash,
│                            Progression/ExperienceCurve, Characters/TestHero/ (Char_TestHero…),
│                            Characters/TrainingDummy/ (Enemy_TrainingDummy : 1000 PV, 30 armure/RM, 150 XP),
│                            Characters/Enemies/ (Enemy_Goblin, Enemy_SkeletonArcher, Enemy_Brute + StatBlock / BasicAttack),
│                            Spells/Enemies/ (Spell_BruteSlam, Spell_GrokkSlam, Spell_GrokkNova), Characters/Bosses/ (Boss_GoblinKing, Pattern_GoblinKing), Run/ (WaveRewardSchedule, TestStage/Stage_Test + Wave_01…05, BonusWave_01…02),
│                            Economy/ (ShopCatalog, Items/, Elixirs/, Anvils/, StatChoices/),
│                            Passives/ (Passive_…), Augments/ (AugmentCatalog, Augment_…), Spells/Summoner/ (Ghost, Ignite),
│                            Settings/LootSettings
├── GameAssets/ActionMaps/   Game.Input — GameActionMaps (.inputactions + C# généré)
├── Prefabs/                 RootLifetimeScope, ClickMarker, Characters/ (Character, Player, TrainingDummy, Enemy, Boss),
│                            Combat/ (Projectile_ArcaneBolt, Projectile_Skillshot, Vfx_SpellShape)
├── GraphicAssets/           Sprites/ (Characters/…, Items/…, Augments/…, Spells/ (Flash, Ezreal_*), Status/…, UI/White, VFX/Arcane_Bolt), Materials/Unlit_Sprite
├── Scenes/                  Releases/Menu, Tests/TestScene (+ Releases/Game : ancienne scène WASD, hors build)
├── Tests/                   EditMode/, PlayMode/
└── UI Toolkit/   Settings/
Docs/                        CharacterSystem_Design.md
```

Les nouveaux modules du §10 (Combat, Spells, Items, Run, AI…) vont dans `Features/<Module>/`, et leurs données dans `Data/<Domaine>/`.

**Assembly definitions** : `Game.Input` ← `Game.Systems` ← `Game.Features` ← `Game.Containers` ← `Game.Editor` (éditeur seulement) ; `Game.Tests.EditMode` / `Game.Tests.PlayMode`. Règle : **`Systems` ne référence jamais `Features`**. Séparer `Features` en plusieurs asmdef par module quand le projet grossit.

### 14.7 Git

**Réglages Unity (une fois)** : *Version Control Mode = Visible Meta Files* ; *Asset Serialization = Force Text*.

**À versionner** : `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/`, `Docs/`. **À ignorer** : `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, builds, fichiers IDE (`.gitignore` fourni).

**Fichiers fournis** : `.gitignore` (Unity + IDE + OS) et `.gitattributes` (normalisation des fins de ligne, **Git LFS** pour textures, audio, modèles 3D, vidéos, polices).

> État actuel : `.gitattributes` force LF sur le code et le YAML Unity, branche Smart Merge sur les assets YAML et marque les assets lourds en `binary`. **Git LFS n'est pas encore actif** (`git-lfs` non installé) : `sudo pacman -S git-lfs && git lfs install`, puis remplacer `binary` par `filter=lfs diff=lfs merge=lfs -text` dans `.gitattributes` et `git add --renormalize .`. À faire **avant** le premier asset lourd (sprites, sons).
>
> Smart Merge, une fois par clone :
> ```
> git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
> git config merge.unityyamlmerge.driver "<Editor>/Data/Tools/UnityYAMLMerge merge -p %O %B %A %A"
> git config merge.unityyamlmerge.recursive binary
> ```
> Le NavMesh baké (`NavMesh-Ground.asset`) est binaire : déclaré `binary`, jamais fusionné. Il doit rester un **asset séparé** : embarqué dans la scène, il force Unity à l'enregistrer en binaire.

**Workflow**
- Branches : `main` (stable, taguée par jalon, ex. `v0.1-phase1`), `dev` (intégration), `feature/<module>-<sujet>`, `fix/<sujet>` (minuscules, tirets, pas de `&` ni d'espace). Fusion par **pull request** vers `dev`.
- **Commits atomiques** au format *Conventional Commits* : `feat(stats): ajoute les StatModifier`, `fix(nav): corrige le clic hors NavMesh`, `data(spells): ajoute Spell_Fire_x1`, `docs: met à jour le design doc`, `refactor`, `test`, `chore`.
- **Toujours committer un asset avec son `.meta`**. Déplacer et renommer les assets **dans Unity** uniquement.
- **Limiter les conflits de scènes** : scènes petites, logique dans les **prefabs** et les ScriptableObjects, scènes additives ; une seule personne édite une scène à la fois.
- **Smart Merge** : configurer `UnityYAMLMerge` comme outil de fusion (`git mergetool`) dans le `.gitconfig`.
- Un jalon = une **phase du §11**, avec un tag.

### 14.8 Revue du code existant (mise en conformité du 12/10/2026)

| Problème trouvé | Correction |
|---|---|
| **Deux `EventBus` et deux `InputReader`** (un par scope de scène) : les événements du menu n'atteignaient pas la scène de jeu | `RootLifetimeScope` unique (prefab + `VContainerSettings`) ; les scopes de scène n'enregistrent plus que leurs composants |
| Insertion de `Loading`/`Win` **au milieu** de `GameState` : les règles sérialisées de la scène Menu étaient décalées, `Menu → Game` refusé, **bouton Play cassé** | Règles du §14.2 sur le prefab root ; note « ajouter les états à la fin » (§14.2) |
| `MenuLifetimeScope` enregistrait un `GameHUD` **null** (absent de la scène) → exception à la construction du scope | Retiré |
| `QuitRequestedEvent` publié par le menu mais **écouté par personne** | Géré par `GameFlowController` |
| `PanelManager` **statique** (état mutable global) | Service `IPanelManager` injecté |
| `GameHUD` ouvrait le nouveau panneau avec `toOpen: false` (le fermait) | Corrigé ; injection par méthode |
| `InputReader.Initialize` appelé de deux endroits, lambdas jamais désabonnées | Entry point `IInitializable`/`IDisposable` du root, handlers nommés, maps pilotées par `InputMapSwitcher` |
| `PointerCommandEvent.WorldPosition` toujours `Vector3.zero` | Champ supprimé (position écran seulement) |
| `InputsEventBus` **statique** + déplacement WASD (`MovingCharacter`, `CameraOrientedMovingCharacter`, `PlayerCharacterController`), `UIEventBus` vide | Supprimés |
| `Game.Containers` (référence `Features`) rangé dans `Systems/` ; code de jeu (caméra, mouvement, clic) dans `Systems` | Déplacés (§14.6), GUID conservés (aucune référence de scène cassée) |
| Réglages caméra en champs sérialisés | SO `CameraSettings` injecté |
| `Camera.main` / `GetComponent` en fallback, `Instantiate` direct du marqueur | Références câblées, `IObjectResolver.Instantiate` |
| `CharacterMovement` écrasait la vitesse verticale (gravité) et ne synchronisait pas l'agent | `y` conservé, `agent.nextPosition = rb.position` |
| Perso saccadé : Rigidbody non interpolé, rotation écrite sur le `transform` dans `Update` | `RigidbodyInterpolation.Interpolate` imposé, rotation via `rb.MoveRotation` en `FixedUpdate` |
| Bouton Quit qui pouvait quitter seul au lancement (IMGUI réagit au bouton maintenu) | Clic lu via `ConfirmEvent` (appui), publie `QuitRequestedEvent` |

Points restants (connus) :
- `Releases/Game.unity` : ancienne scène WASD, scripts manquants, retirée du build. À supprimer ou à reconstruire quand le vrai stage arrivera.

**Outillage**
- *Tools > Project Setup* (`EditorTools.ProjectSetup.Run`) : crée / répare le prefab root, ses règles de transition, les SO de réglages, `VContainerSettings`, la liste de scènes du build. Idempotent.
- *Tools > Create Test Scene* / *Validate Test Scene* : régénère / vérifie `TestScene`.
- *Tools > Add Test Obstacles* : (re)crée le parcours de pathfinding de `TestScene` (mur, angle en L, piège en U, piliers, couloir étroit) et rebake le NavMesh, sans toucher au reste de la scène.
- *Tools > Build Linux (Test Scene)* / *(Game)* : builds standalone.
- *Tools > Regenerate Input Actions Code* : régénère `GameActionMaps.cs` (§4.7).
- Tests : `Game.Tests.EditMode` (stats, `EventBus`, state machine, disposition clavier, `CharacterLevel`, formules de dégâts, `Health`, `ManaPool`, rangs des sorts (`AbilityRanks`), haste, cône, dégâts de sort par rang et ratios, modificateurs temporaires), `Game.Tests.PlayMode` (lancement direct de `TestScene`, Menu → Play → `TestScene`, contournement des obstacles, caméra, init du joueur et montée de niveau via le bus, spawn depuis le prefab, billboard et flip du sprite, attaque d'un dummy, kill → XP → respawn, projectiles poolés, attack-move, cercle de portée, rangs des sorts d'Ezreal (points, niveaux requis, « + »), Q visé sur un dummy (dégâts, mana, délai réduit à l'impact), W qui marque puis explose sur une attaque (mana rendu), E (blink + trait), quick cast, quick cast avec indicateur (maintien → visée, relâchement → lancer, Échap pendant le maintien, mode Normal), Flash à distance fixe puis en cooldown, Flash dans un mur, gobelin qui poursuit et frappe, contournement d'un mur par le flow field, archer qui garde sa portée, brute qui lance son sort à mana plein, kill → XP + gold → retour au pool → réutilisation, mort du joueur → horde vidée → respawn, spawn continu borné à `MaxAlive`, vague : compte à rebours → combat → quota (XP coupée, gold gardé) → timer → récompense en pause → vague suivante, overtime → vies perdues → défaite → Rejouer, mort = game over immédiat, toutes les vagues + bonus → victoire, enclume prismatique gratuite après la vague 2 puis boutique légère et enclume à stats, draft d'augment après la vague 1, passif d'Ezreal (stacks de vitesse d'attaque), Frénésie (stacks puis chute), `SpellCrit`, sort d'invocateur ramassé puis utilisé, enclume ramassée ouverte en fin de vague, boss (items, sort de la phase 1, phases à 60 % et 30 %, mort), vague de boss (overtime tant qu'il vit, fin à sa mort), Échap → visée annulée puis pause → options → reprise → menu principal, Paramètres du menu → options), plus `UserOptions` (mode de lancement par défaut, sauvegarde / rechargement JSON), le flow field (ligne droite, contournement, coins, cellule enfermée) le `Wallet`, le `WaveRewardSchedule` et la boutique (limites par boutique, enclumes à stats et à items, rerolls, inventaire plein, élixir jusqu'à la fin de la vague suivante, enchaînement des récompenses) et les augments (draft, relances, relance dorée, slots pleins : remplacement, slot puis gold, enclume) et le point visé à portée fixe en EditMode.

### 14.9 Actions d'entrée à prévoir (Input System)

Map **Gameplay** (les actions `Move`, `Look`, `Jump`… du template ne sont plus utilisées, à nettoyer) :

| Action | Type | Binding par défaut |
|---|---|---|
| `PointerPosition` | Value (Vector2) | souris |
| `PointerCommand` | Button | clic droit (déplacement ou attaque) |
| `Confirm` | Button | clic gauche (valide une visée) |
| `Cancel` | Button | Échap (annule la visée, sinon ouvre la pause) |
| `AttackMove` | Button | position A (Q en AZERTY ; maintenu ; `AttackMoveEvent.Pressed` vrai à l'appui, faux au relâchement) |
| `Ability1` … `Ability4` | Button | positions Q W E R (A Z E R en AZERTY ; appui et relâchement) |
| `SummonerD`, `SummonerF` | Button | D, F (appui et relâchement) |
| `Stop` | Button | S |
| `LevelUpModifier` | Button | Ctrl gauche / droit (maintenu avec un sort : dépense un point) |
| `CameraLock` | Button | Espace, clic molette |
| `Zoom` | Value (Vector2) | molette |

Les bindings du groupe `Positional` (sorts, invocateurs, attack-move, stop) restent à leur position ; les autres lettres sont remappées selon `ControlsSettings.KeyboardLayout` (§4.7). Prévoir le **rebind** (`UserOptions`).
