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
2. **Data-driven** : tout ce qui diffère d'un perso à l'autre vit dans des ScriptableObjects (stats de base, sprite, éléments et grimoire de départ…).
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
│   ├── ElementCaster        → stacks d'éléments (A/Z/E), reset (T), lancer (R), coût en mana
│   ├── SignatureAbility     → compétence propre du personnage (F, cooldown seul), remplaçable par un sort d'invocateur ramassé
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
                                                  └─►  ElementCaster (éléments + grimoire de départ)
ICommandSource ──► CommandController ──┬─► CharacterMovement ──► Rigidbody
                                       ├─► BasicAttacker ──► DamageInfo ──► DamageReceiver (cible) ──► Health
                                       └─► ElementCaster ──► SpellResolver ──► Spell.Cast(CastContext) ──► DamageInfo ──► ...
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
| `Enemy` (variant, phase 6) | + IA |

Le prefab de base porte aussi `BasicAttacker` et une **barre de PV** (`HealthBarView` : deux sprites face caméra au-dessus de la tête, verte pour le joueur, rouge pour les ennemis ; en sprites plutôt qu'en UI pour rester léger avec des centaines d'entités).

- Ce qui distingue deux persos d'un même type (**sprite**, **taille du corps** `BodySize`, stats…) vient de la `CharacterDefinition`, appliquée par `Character.Init` : jamais un variant par personnage.
- **`CharacterVisual`** : billboard (copie la rotation de la caméra, donc le sprite est vu à plat, sans déformation de perspective) et `flipX` selon le déplacement horizontal **à l'écran**. Les sprites sont dessinés **tournés vers la droite**, pivot en bas au centre. *Pour les centaines d'ennemis : un `LateUpdate` par entité est à remplacer par une mise à jour centrale (phase 6).*
- Le joueur n'est **pas posé dans la scène** : `PlayerSpawner` (entry point du `GameLifetimeScope`) l'instancie via `IObjectResolver.Instantiate` au `PlayerSpawn`, appelle `Init` et publie `PlayerSpawnedEvent`, que la caméra écoute pour le suivre.
- Sprites provisoires (64 × 128, 64 px/unité, `Assets/GraphicAssets/Sprites/Characters/`) : `Placeholder_Hero` (généré par *Project Setup*, aperçu du prefab de base) et **`Chibi_Explorer`**, utilisé par `Char_TestHero` : chibi pixel-art *inspiré* d'Ezreal (League of Legends). Placeholder de prototype uniquement : à remplacer par une art originale avant toute diffusion publique (propriété intellectuelle de Riot Games).

---

## 4. Ordres, déplacement et attaque de base (façon LoL)

### 4.1 Contrôles

| Input | Effet |
|---|---|
| **Clic droit sur le sol** | `MoveCommand(point)` |
| **Clic droit sur un ennemi** | `AttackTargetCommand(ennemi)` : le perso se rapproche jusqu'à sa portée puis attaque en boucle |
| **Maintenir Q** + **clic gauche** | *Attack-move* (§4.6) : affiche la portée d'attaque tant que Q est maintenu ; le clic lance la marche vers le point en attaquant ce qui croise sa route |
| **A / Z / E** | Ajoute un stack de l'élément 1 / 2 / 3 (§7) |
| **Ctrl + A / Z / E** | Dépense un point de compétence sur l'élément (le débloque au premier point, §7.8) ; aussi via le bouton « + » du HUD |
| **R** | Lance le sort correspondant aux éléments actifs (visée puis clic gauche, §7.4) |
| **F** | Compétence propre du personnage (visée puis clic gauche, §7.8) |
| **T** | Reset : vide la sélection d'éléments |
| **S** | `StopCommand` (annule l'ordre en cours) |
| **Échap / clic droit** pendant une visée | Annule la visée |
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
- **Windup** : délai avant l'impact (`WindupPercent × intervalle`). Un nouvel ordre de déplacement **annule** l'attaque pendant le windup (comme LoL : l'attaque est perdue). Après l'impact, on peut bouger sans perdre la cadence.
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

**Choix retenu** : les bindings des lettres sont écrits pour la **lettre voulue à sa position QWERTY** (`<Keyboard>/q` = attack-move…), puis **remappés au démarrage** selon la disposition du joueur : réglage `KeyboardLayout` (`Qwerty` / `Azerty`, **AZERTY par défaut**) dans le SO `ControlsSettings` (`Assets/Data/Settings`), appliqué par `InputReader` via des *binding overrides* (A ↔ Q, Z ↔ W, M → `;`). Vérifié par `KeyboardLayoutTests` à travers le vrai `InputReader`.

**Rejeté** : les bindings par nom affiché (`<Keyboard>/#(a)`). Le player Linux ne remonte pas la disposition de l'OS (il retombe sur les noms QWERTY : en jeu, « Q » se retrouvait sur la touche A), et l'éditeur Linux segfault en `-batchmode -nographics` en les résolvant.

À venir : le choix du joueur (`UserOptions`, JSON) par-dessus `ControlsSettings`, puis un **menu de rebind** complet.

> **Wrapper C#** : `GameActionMaps.cs` est **généré** par l'Input System depuis le `.inputactions`. Après toute modification des actions : *Tools > Regenerate Input Actions Code*, et committer les deux fichiers ensemble. (L'ancien fichier était un stub écrit à la main que Unity refusait d'écraser ; le `.meta` de l'importer avait aussi des noms de champs invalides, ce qui désactivait la génération.)

---

### 4.8 Caméra (en place)

`CameraController` (Features/CameraControl), réglages dans le SO `CameraSettings` (`Assets/Data/Settings`).

- **Verrouillée** (défaut, `StartLocked`) : suit le joueur avec un offset fixe, lissé (`FollowSpeed`).
- **Libre** : le joueur la déplace en amenant le curseur au **bord de l'écran** (`EdgeSize` px, vitesse proportionnelle à la profondeur dans la zone, lissée par `ScrollSmoothing`), sur le plan XZ, comme dans LoL.
- **Toggle** : `CameraLockEvent` (Espace / clic molette). **Zoom** : `ZoomEvent` (molette), borné par `MinZoom` / `MaxZoom`.
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

> **En place** : `TimedModifiers` (`Character.Buffs`, tick chaque frame) : réappliquer depuis la même source sur la même stat **rafraîchit** (valeur + durée) au lieu de cumuler ; des sources différentes se cumulent. *Reste : CC (stun, root, silence…) et ténacité (§5.7).*

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

### 5.7 Contrôles (CC) et Ténacité

Types : `Stun`, `Root`, `Silence`, `Slow`, `Knockup`. `StatusEffect` calcule la durée effective :
```
duréeEffective = duréeBase × (1 − Tenacity)        // Tenacity clampée (ex. max 0.9)
```
S'applique aux CC **et aux ralentissements** (un Slow = `StatModifier` PercentAdd négatif sur `MoveSpeed`, retiré à l'expiration). `SlowResist` (optionnel) réduit en plus l'intensité du ralentissement.

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

> **En place** : `DamageInfo` (struct), `DamageCalculator` (formules pures, testées), `Health` (PV liés à `MaxHealth` : un gain de max ajoute autant de PV courants, une baisse les borne ; régénération `HealthRegen`/s ; mort ; résurrection), `Character.TakeDamage` (résistances → PV → sustain de l'attaquant). Chiffres de dégâts (`DamageNumbersView`) : jaunes, plus gros et suivis de « ! » en crit, rouges quand le joueur est touché. *Reste : bouclier, flash blanc, hit-stop, SFX ; mort du joueur (`PlayerDiedEvent` publié, rien n'y réagit avant la boucle de run).*

### Cooldowns & Ability Haste
```
cooldownEffectif = cooldownBase × 100 / (100 + AbilityHaste)
```
S'applique au cooldown de chaque lancer de sort élémentaire et à la compétence propre (§7.7). Le coût en mana n'est **pas** affecté par le haste.
`AttackSpeed` : `intervalle = 1 / (AttackSpeedBase × AttackSpeedStat)`.

---

## 7. Éléments, mana et sorts

### 7.1 Principe

Le joueur **prépare** un sort en empilant des éléments, puis le **déclenche**.

| Touche | Action |
|---|---|
| **A / Z / E** | Ajoute **un stack** de l'élément du slot 1 / 2 / 3 (instantané, gratuit) |
| **R** | Lance le sort correspondant à la sélection (impossible sans élément) |
| **F** | Lance la **compétence propre** du personnage (indépendante des éléments, §7.8) |
| **T** | Reset : vide la sélection d'éléments (et annule une visée de R en cours) |

- Un même élément peut être empilé **plusieurs fois** (A, A, A) : le sort garde la même nature mais en **version plus grande** (§7.5).
- **L'ordre d'activation ne compte pas** : Feu + Eau = Eau + Feu.
- **Plus il y a de stacks, plus le lancer coûte de mana** (§7.3).
- **Après un lancer, la sélection se réinitialise** automatiquement. **T** sert à repartir de zéro **avant** de lancer, si le joueur se ravise.
- Déplacement, attaque de base et Q + clic gauche restent **identiques** (§4).

### 7.2 Données (ScriptableObjects)

```csharp
[CreateAssetMenu(menuName = "Game/Element")]
public class ElementDefinition : ScriptableObject {
    public int Id;                 // 0..31, sert à construire le masque d'éléments
    public string DisplayName;
    public Sprite Icon; public Color Color;
    public float ManaCost;         // coût de base d'UN stack de cet élément
}

[CreateAssetMenu(menuName = "Game/Spell")]
public abstract class SpellDefinition : ScriptableObject {
    public string DisplayName; public Sprite Icon;
    public ElementCount[] Recipe;                 // ex. { Feu×2, Eau×1 } : une recette = un sort
    public TargetingType Targeting;               // Self, Skillshot, GroundPoint, Cone, UnitTarget
    public float Range, CastTime;
    public float CooldownOverride;                // 0 = table CooldownByStacks (§7.7)
    public SpellKind Kind;                        // Magic, Physical, Buff (§7.6)
    public float PowerRatio;                      // ratio de dégâts, ou puissance du buff / soin
    public SpellScaling Scaling;                  // valeur qui évolue avec le niveau des éléments du sort (§7.6)
    public bool CanMoveWhileCasting;
    public abstract Spell CreateRuntime(Character owner);     // fabrique l'instance runtime
}

[Serializable] public struct ElementCount { public ElementDefinition Element; public int Count; }
[Serializable] public struct SpellScaling { public float Base; public float PerElementLevel; }
// valeur = Base + PerElementLevel × Σ (niveau de l'élément − 1), sur les éléments DISTINCTS de la recette

[CreateAssetMenu(menuName = "Game/SpellBook")]
public class SpellBook : ScriptableObject {
    public List<SpellDefinition> Spells;          // une entrée par recette (jusqu'à 19 pour 3 éléments)
}
```

**Sélection courante** (état du joueur, pas un SO) :

```csharp
public class ElementSelection {
    public int[] Stacks;           // un compteur par slot A/Z/E
    public int Total;              // somme des stacks
    public int Key;                // Σ Stacks[i] × 4^i  → identifie la recette (0..63)
    public bool CanAdd(int slot);  // Total < MaxTotalStacks et Stacks[slot] < MaxStacksPerElement
    public void Add(int slot); public void Clear();
}
```

- La **recette** est le nombre de stacks par élément (`Key`) : **l'ordre d'activation n'a aucune influence** (Feu puis Eau = Eau puis Feu), mais Feu×2 + Eau et Feu + Eau×2 sont **deux recettes différentes**.
- **Une recette = un sort distinct** : pas d'échelle automatique par stack, le design décide de ce que fait chaque combinaison (à confirmer §13).
- Le nombre total de stacks (`Total`) détermine le **coût en mana** (§7.3) et le **cooldown** (§7.7).
- **Limites** (`SpellSettings`) : **`MaxTotalStacks = 3`** (un sort = 3 éléments maximum) ; `MaxStacksPerElement = 3`. Avec 3 éléments : **19 recettes** (3 de 1 stack, 6 de 2, 10 de 3).
- `SpellBook.Init(slotElements)` convertit chaque recette en `Key` selon l'ordre A/Z/E du personnage. Une validation éditeur (`OnValidate`) signale les recettes manquantes ou en double.

**Données / runtime** : un SO est **partagé**, donc on n'y stocke jamais de cooldown. Le cooldown en cours vit dans l'instance runtime `Spell`, créée par `CreateRuntime`.

> **En place (phase 5b)** : `ElementDefinition`, `SpellDefinition` (pour l'instant : nom, icône, `SpellKind`, recette ; ciblage, cast, puissance et runtime `Spell` arrivent en 5c), `SpellBook`, `SpellSettings` (`MaxTotalStacks`, `MaxStacksPerElement`, `CostCurve`, `MaxElementLevel`, enregistré dans le root), `ElementSelection` (clé `Σ stacks × 4^i`), `SpellResolver` (clé → sort selon l'ordre A/Z/E **du perso** ; signale doublons, éléments étrangers et recettes manquantes ; calcule le coût). `CharacterDefinition.OnValidate` vérifie que le grimoire couvre les 19 recettes. Contenu de test : `Elem_Fire` (10 mana), `Elem_Electricity` (12), `Elem_Wind` (8), 19 sorts nommés et `SpellBook_FireElectricityWind` (`Assets/Data/Elements`, `Assets/Data/Spells/FireElectricityWind`).

### 7.3 Mana et coût

`ManaPool` : `Current`, `Max` (= stat `MaxMana`), régénération (`ManaRegen`/s), events `Changed`.

```
N      = nombre total de stacks
base   = Σ (stacks[i] × element[i].ManaCost)
mult   = CostCurve[N]                              (ex. 1 → ×1, 2 → ×1.5, 3 → ×2.25)
coût   = base × mult × (1 − ManaCostReduction)
```

Exemples (Feu 10, Eau 10, Terre 12) :
- Feu ×1 → `10 × 1 = 10`
- Feu + Eau → `20 × 1.5 = 30`
- Feu ×3 → `30 × 2.25 = 67.5`
- Feu + Eau + Terre → `32 × 2.25 = 72`

La courbe croît plus vite que le nombre de stacks pour que chaque ajout coûte vraiment plus. `CostCurve` est un tableau dans un SO de réglages (`SpellSettings`) : équilibrage sans recompiler.

- Le coût est **calculé en direct** pour l'UI (prévisualisation sur la barre de mana).
- Le mana est **débité à la confirmation du cast** (clic gauche), pas à l'appui sur R. S'il est insuffisant à ce moment : cast refusé + feedback, **la sélection est conservée**.

> **En place** : `ManaPool` (sur chaque `Character` : max = `MaxMana`, un gain de max ajoute autant de mana courant, régénération `ManaRegen`/s), coût calculé en direct (`SpellResolver.ManaCost`, avec `ManaCostReduction`) et **prévisualisé sur la barre de mana** du HUD (portion claire, rouge si le mana manque). Le débit au cast arrive en 5c.

### 7.4 Machine à états du lanceur

```
 A/Z/E ──► ajoute un stack (si CanAdd), à tout moment

 Idle ──[R, Total ≥ 1, sort prêt, mana OK]──► Targeting ──[clic gauche]──► Casting ──► Idle + sélection vidée
                                                  │
                          [clic droit / Échap]  → Idle (sélection conservée)
                          [T]                   → Idle (sélection vidée)
```

- **R sans élément** → rien (feedback "aucun élément"). **R sur cooldown ou sans mana** → feedback aussi.
- **A/Z/E sur un slot verrouillé** → aucun stack, feedback ("Élément non débloqué").
- Sort de type `Self` → lancé immédiatement sans visée.
- **Ajouter un stack pendant la visée** : le sort est **re-résolu en direct** (indicateur, taille, portée et coût mis à jour).
- **Pas de retrait individuel** : on ne peut pas enlever un seul stack, seul **T** vide la sélection (voir §13).
- **Un seul mode de visée à la fois** : Q (attack-move), R (sort élémentaire) et F (compétence propre) sont exclusifs ; appuyer sur l'un remplace l'autre. A/Z/E/T ne sont pas des modes de visée, donc ne coupent pas Q ni F. La **compétence propre (F)** ne consomme ni ne vide la sélection d'éléments.
- **Après un cast réussi** : la sélection est **vidée automatiquement** (`ClearAfterCast = true`).
- **Pendant un cast** (windup) : le joueur peut déjà empiler pour le sort suivant, le sort en cours est figé.
- **Quick cast** (option) : R lance directement vers le curseur sans passer par la visée.

> **En place (phase 5c)** : `ElementCaster.TryCast` (R) vérifie sélection, sort, cooldown, mana et incantation, puis vise via `SpellTargeting` (ou lance aussitôt un sort `Self`). Clic gauche : le sort résolu **à cet instant** est lancé (il suit les éléments ajoutés pendant la visée), le mana est débité (refus + sélection conservée s'il manque), le cooldown démarre, la sélection est vidée. Clic droit / Échap : visée annulée, sélection conservée ; T : visée annulée et sélection vidée. Exclusivité Q / R / F par `AimModeEvent`. Pendant une incantation (`SpellCaster`, sauf `CanMoveWhileCasting`), les ordres de déplacement / attaque sont ignorés ; on peut empiler des éléments pour le sort suivant. *Reste : quick cast, buffer d'input, stun / silence.*

### 7.5 Résolution du sort et visée

`SpellResolver.Resolve(key) → SpellDefinition` : lookup dans le dictionnaire du `SpellBook`. Recette sans sort → R inactif avec feedback (la validation éditeur doit l'éviter).

**Grimoire d'un personnage à 3 éléments (19 sorts)**

| Total de stacks | Recettes | Nombre |
|---|---|---|
| 1 | A, Z, E | 3 |
| 2 | A×2, Z×2, E×2, A+Z, A+E, Z+E | 6 |
| 3 | A×3, Z×3, E×3, A×2+Z, A×2+E, Z×2+A, Z×2+E, E×2+A, E×2+Z, A+Z+E | 10 |

Chaque case est un `SpellDefinition` de type **Magic**, **Physical** ou **Buff** (§7.6). Exemple pour Feu / Eau / Terre : Feu = boule de feu (Magic) ; Terre = pic de pierre (Physical) ; Eau + Terre = armure de boue (Buff) ; Feu + Eau + Terre = éruption (Magic, grosse zone).

**Pointage souris → monde (plan XZ)** (réutilisé pour Q, R et le clic droit) :
```csharp
Plane ground = new Plane(Vector3.up, Vector3.zero);
Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
if (ground.Raycast(ray, out float d)) targetPoint = ray.GetPoint(d);
```

**Types de ciblage** (`SpellDefinition.Targeting`) :
| Type | Indicateur | Exemple |
|---|---|---|
| `Self` | aucun (cast direct) | bouclier, aura |
| `Skillshot` | ligne/rectangle depuis le perso | projectile |
| `GroundPoint` | cercle au curseur, portée max | zone au sol |
| `Cone` | secteur devant le perso | souffle |
| `UnitTarget` | surligne l'ennemi sous le curseur | sort ciblé |

> **En place** : `SpellTargeting` + `SpellIndicator` dessinent la portée et la forme sous le curseur (rectangle d'un skillshot, cercle d'une zone, secteur d'un cône, anneau sous la cible d'un sort ciblé ; « Aucune cible » / « Cible hors de portée » sinon). `SpellExecutor` applique l'effet selon `Targeting` : **skillshot** (`SkillshotProjectile` en ligne droite, s'arrête au premier ennemi ou traverse avec `Pierce`, peut rater), **zone** (tous les ennemis dans `Radius`), **cône**, **ciblé** (projectile à tête chercheuse), **Self** ; flash de forme au sol aux couleurs du sort (`SpellVfx`), projectiles teintés, tout est poolé (`PrefabPool`).

```csharp
public struct CastContext {
    public Vector3 Origin;
    public Vector3 Direction;      // normalisée, plan XZ
    public Vector3 TargetPoint;    // clampé à la portée max
    public Character TargetUnit;   // si UnitTarget
}
// Spell.Cast(CastContext ctx)
```

### 7.6 Types de sorts : magique, physique, buff

`SpellKind` détermine **comment le sort se comporte dans le pipeline de dégâts** (§6) :

| Kind | Type de dégâts | Stat de scaling | Pénétration / défense | Effet |
|---|---|---|---|---|
| **Magic** | `DamageType.Magic` | `Base(niveaux d'éléments) + MagicAttack × PowerRatio` | `MagicPen` flat/% contre `MagicResist` | dégâts |
| **Physical** | `DamageType.Physical` | `Base(niveaux d'éléments) + PhysicalAttack × PowerRatio` | `ArmorPen` flat/% contre `Armor` | dégâts |
| **Buff** | aucun | `MagicAttack` optionnel (soin/bouclier) | — | stats temporaires, soin, bouclier |

- **Magic / Physical** : même mécanique, seule la paire stat/résistance change. Crit **uniquement** si la capacité `SpellCrit` est active (§5.6). **Omnivamp** s'applique aux deux ; **Lifesteal** reste réservé aux attaques de base (§6).
- **Buff** : liste d'effets appliqués au lanceur ou aux alliés dans la zone :
  ```csharp
  public struct BuffEffect { public StatType Stat; public ModifierType Type; public float Value; public float Duration; }
  // + Heal / Shield optionnels (multipliés par HealShieldPower, §6)
  ```
  Chaque effet devient un `StatModifier` temporaire (§5.4, `Source` = le sort, pour pouvoir le retirer ou le rafraîchir). Chaque recette de buff définit ses propres valeurs et durées.
- **Évolution avec le niveau des éléments** : la valeur d'un sort (dégâts, soin, valeur du buff, durée) vaut `Base + PerElementLevel × Σ (niveau − 1)` calculé sur les **éléments distincts** de sa recette, en plus du scaling sur les stats. **Un même élément compte une seule fois** par sort : Feu ×3 ne profite du niveau de Feu qu'une fois. Le niveau d'un élément vient des points de compétence (§7.8).
- Un sort **mixte** (dégâts + buff) est possible : `SpellDefinition` peut porter une liste d'effets, mais le `Kind` principal décide du scaling.

> **En place** : dégâts = `Scaling.Evaluate(Σ niveaux − 1 des éléments distincts)` + `MagicAttack` (ou `PhysicalAttack`) × `PowerRatio`, contre `MagicResist` (ou `Armor`) avec la pénétration du lanceur ; omnivamp oui, lifesteal non ; pas de crit (capacité `SpellCrit` à venir). Buffs : `BuffEffect` → `Character.Buffs` (Source = le sort, donc relancer rafraîchit). *Reste : soins et boucliers, buffs sur les alliés, valeurs de buff qui évoluent avec le niveau des éléments.* Les 19 sorts du perso de test ont des valeurs **provisoires** (Assets/Data/Spells) ; le héros a 40 de `MagicAttack` de base.

### 7.7 Cooldown, cast et ordres

- **Cooldown par lancement** : après chaque cast de R, un **cooldown commun** à tous les sorts élémentaires démarre ; sa durée dépend du **nombre de stacks du sort lancé** : `CooldownByStacks = {2, 3.5, 5.5}` s (dans `SpellSettings`), donc un sort à 1 élément est rapide à relancer, un sort à 3 éléments beaucoup moins. Surcharge possible par sort (`CooldownOverride`). Réduit par `AbilityHaste` (§6).
- **Compétence propre (F)** : son **propre cooldown**, indépendant de celui de R (Flash : 20 s).
- **Cast time** : windup avant l'effet ; interrompt le déplacement/l'attaque en cours sauf `CanMoveWhileCasting`. Après le cast, le perso redevient `Idle`.
- **Les éléments restent modifiables pendant un cast** (préparation du sort suivant), mais le sort en cours est déjà résolu et figé.
- Un **stun/silence** bloque R (pas A/Z/E).

> **En place** : cooldown commun dans `ElementCaster` (`SpellSettings.CooldownByStacks` ou `CooldownOverride`, × 100 / (100 + `AbilityHaste`)) ; cooldown propre de F dans `SignatureAbility` ; `CooldownStartedEvent` → HUD (temps restant sur la ligne du sort, compte à rebours sur le slot F). *Reste : stun / silence (CC).*

### 7.8 Identité d'un personnage, points de compétence et éléments

**Éléments du personnage** : chaque perso a **exactement 3 éléments** (personnage de test : **Feu, Électricité, Vent**), chacun avec **sa touche fixe** selon son ordre dans la définition (A = élément 1, Z = élément 2, E = élément 3).

> **En place** : `ElementLevels` (points dépensés via `CharacterLevel.TrySpendSkillPoint`, verrous 1/3/5 lus dans `CharacterDefinition.ElementUnlockLevels`, niveau max `SpellSettings.MaxElementLevel`). Entrée : **Ctrl + A/Z/E** ou bouton « + » du HUD. Feedback si refusé (« Aucun point de compétence », « Prochain élément au niveau 3 », « Niveau maximum »). *Reste : surcharge (méta, §9.7).*

**Points de compétence (comme LoL)** : **1 point par niveau de 1 à 15**. Les **niveaux 16, 17 et 18 n'augmentent que les stats**, sauf pour un élément dont la **surcharge est débloquée** dans l'arbre méta (§9.7) : ils donnent alors des points de **surcharge** (ci-dessous). Les points servent **uniquement à monter le niveau des éléments** :
- Un élément est à **niveau 0 (verrouillé)** ; y mettre un premier point le **débloque** (niveau 1), puis chaque point supplémentaire monte son niveau (`MaxElementLevel = 5` : 3 × 5 = 15 points, soit exactement les niveaux 1 à 15).
- **Verrous façon LoL** : on peut débloquer **un élément dès le niveau 1**, un **deuxième à partir du niveau 3**, le **troisième à partir du niveau 5** (`ElementUnlockLevels = {1, 3, 5}`). Aux niveaux 1 et 3 le joueur **choisit** quel élément débloquer ; au niveau 5 il ne reste que le dernier.
- Les points non dépensés sont conservés.
- **Surcharge** (niveaux 16, 17, 18) : un point de surcharge ne peut être dépensé que sur un élément **déjà au niveau max** et dont la **surcharge est débloquée dans l'arbre méta de l'élément** (§9.7) ; sinon il est perdu. **Un élément ne peut être surchargé qu'une seule fois** ; la surcharge **augmente le multiplicateur de dégâts de l'élément** (`OverchargeMultiplier`, toujours **une seule fois par sort**), avec éventuellement un rendu distinct dans le sort (couleur, par exemple). 3 éléments, donc **3 surcharges au maximum**.
- Le niveau d'un élément **augmente les dégâts (ou la valeur) des sorts qui l'utilisent**, **une seule fois par sort**, même si l'élément est empilé 3 fois (§7.6).

| Niveau du perso | Éléments déblocables | Recettes disponibles |
|---|---|---|
| 1 | 1 | 3 |
| ≥ 3 | 2 | 9 |
| ≥ 5 | 3 | 19 |

- **Slot verrouillé** : la touche n'ajoute aucun stack et affiche un feedback.
- **Grimoire par personnage** : un `SpellBook` contient les 19 sorts des 3 éléments du perso. Deux persos qui partagent les mêmes éléments peuvent partager le même asset.

**Ce qui distingue deux personnages :**

| Ce qui varie | Où c'est défini |
|---|---|
| **Les 3 éléments** | `CharacterDefinition.Elements` |
| **Compétence propre** | `CharacterDefinition.SignatureAbility` |
| **Passif** | `CharacterDefinition.Passive` (§8.1) |
| **Attaque de base** : portée, vitesse, dégâts, type, projectile | `BasicAttackDefinition` + stats |
| **Stats de base et croissance par niveau** | `BaseStats` + `StatGrowth` |
| **Grimoire** (19 sorts) | `CharacterDefinition.SpellBook` |

**Compétence propre (F)** : une `SpellDefinition` **hors grimoire**, une par personnage, qui réutilise tout le pipeline (ciblage, `CastContext`, types Magic/Physical/Buff, indicateurs). Elle n'utilise **ni éléments ni stacks**, **cooldown uniquement, aucun coût en mana** (réduit par `AbilityHaste`). **Touche F**, même flux de visée que R. Bloquée par stun/silence comme R. Elle peut être **remplacée temporairement par un sort d'invocateur ramassé** (§9.5).

> **En place** : `CharacterDefinition.SignatureAbility` + `SignatureAbility` (F) : même visée et même incantation que R, cooldown seul, aucun mana. `Spell_Flash` : `GroundPoint`, portée 4,5, `FixedRange` (le point est toujours à la portée complète dans la direction du curseur, `SpellMath.AimPoint`), `Blink` (`SpellExecutor.TryGetBlinkPoint` part de ce point et recule le long de la ligne par pas de `SpellSettings.BlinkTolerance` (0,5) jusqu'au premier point marchable ; téléport via `CharacterMovement.Teleport`). Refusé avec « Destination inaccessible », sans consommer le cooldown, seulement s'il ne reste que la position du perso ; 20 s. *Reste : sort d'invocateur ramassé qui remplace F (§9.5).*

**Personnage de test**
- **Éléments** : Feu, Électricité, Vent.
- **Compétence propre : Flash**, sort `GroundPoint` : téléporte instantanément le perso **toujours à la même distance (sa portée), dans la direction du curseur**, même si le curseur est tout près. **Les murs au milieu ne bloquent pas** ; si le point d'arrivée tombe dans un obstacle (hors NavMesh), le perso atterrit sur le **dernier point marchable** de la ligne avant lui. Refusé seulement s'il n'y en a aucun. **Cooldown : 20 s.**
- **Passif (façon Veigar, « Mal phénoménal »)** : chaque **sort lancé qui touche** donne **1 stack** ; chaque stack donne **+1 `MagicAttack`**. Les stacks **persistent toute la run** (§8.1).

**Attaque de base** : `BasicAttackDefinition` (§4.5) porte `Range`, `WindupPercent`, `ProjectilePrefab`, `DamageRatio` et `DamageType` (Physical par défaut, Magic possible pour un mage). La **vitesse d'attaque**, les **dégâts** et la **portée** effectifs viennent des stats, donc évoluent avec le niveau et les modificateurs.

**Niveaux** : `CharacterLevel` gère XP, niveau et points. `StatGrowth` ajoute un gain par stat à chaque niveau (§5.5).

**Remplacement à chaud** : les éléments sont fixes ; seul `ElementCaster.SetSpellBook(book)` permet de remplacer le grimoire (objets, évolutions).

**Ennemis** : ils utilisent `Spell.Cast` directement via leur IA, sans éléments (§9.6).

### 7.9 Classes et UI

- **Code** : `ElementCaster` (éléments actifs, R/T, validation mana/cooldown), `SpellResolver`, `Spell` (runtime), `ManaPool`, `SpellIndicator` (LineRenderer/decal selon le type), `PlayerCommandInput` (étendu pour A/Z/E/R/F/T), petit **buffer d'input** (~0,15 s).
- **UI** : 3 icônes d'éléments avec **compteur de stacks** (0–3) et touche affichée, **cadenas + niveau requis** sur les slots verrouillés, **nom et icône du sort résolu**, barre de mana avec **prévisualisation du coût**, cooldown du sort, icône de portée de Q.

> **En place** : `ElementCaster` (sur le variant `Player` seulement ; les ennemis lancent sans éléments) relie input, sélection, niveaux et grimoire, et publie `PlayerElementsEvent`, `ElementSelectionChangedEvent`, `ElementLevelsChangedEvent`, `GameplayFeedbackEvent`. HUD (`PlayerHud` + `ElementBarView`) : 3 slots (icône, touche, pastilles de stacks, pastilles de niveau, voile + « Niv. N » si verrouillé, « + » si un point peut être dépensé), nom du sort résolu et coût (rouge si le mana manque), barre de mana avec prévisualisation, messages de feedback. Pas encore d'icônes de sorts ni de cooldown (5c).
---

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

    public ElementDefinition[] Elements = new ElementDefinition[3];   // slots A / Z / E (fixes)
    public int[] ElementUnlockLevels = { 1, 3, 5 };                   // niveau minimum du 1er / 2e / 3e déblocage
    public PassiveDefinition Passive;           // passif du perso (§8.1)
    public SpellDefinition SignatureAbility;    // compétence propre
    public SpellBook SpellBook;                 // les 19 sorts des 3 éléments du perso

    public AIBehaviorDefinition Ai;          // null pour le joueur
    public Faction Faction;                  // Player / Enemy / Neutral
}
```

`Character.Init(CharacterDefinition def)` configure le prefab générique :
sprite, collider, stats, croissance, attaque de base, éléments, grimoire, compétence propre, comportement. **Un prefab, N personnages.**

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
| **Déclenchée** | les attaques de base appliquent un **saignement** ; après un sort à 3 éléments, +MoveSpeed 3 s |
| **À stacks** | **Mal phénoménal** (Veigar : +1 `MagicAttack` par stack, permanent) ; **Enthousiasme** (Jinx) / **rythme critique** (stacks remis à zéro en fin de vague) |
| **Conditionnelle** | gagner de l'AD / AP selon le **nombre d'ennemis autour** (valeur recalculée, sans stacks de sorts) |

**Brique commune `StackCounter`**, configurée **par passif** sur deux axes :

```csharp
public enum StackSource { PerSpellCast /* 1 par sort qui touche */, PerTargetHit, PerKill, PerBasicAttack, Continuous /* valeur calculée */ }
public enum ResetPolicy { Never /* toute la run */, EndOfWave, EndOfStage, AfterDuration }
```

- Les passifs **ne sont pas obligés de stacker sur les sorts** : `Continuous` recalcule périodiquement une valeur (ex. nombre d'ennemis dans un rayon) et met à jour un `StatModifier` (Source = le passif).
- À chaque changement, le passif met à jour ses `StatModifier` ou un multiplicateur lu par les sorts. Événement `Changed` pour l'UI.

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

### 9.2 XP, niveaux, points de compétence et gold

- **XP et gold sont crédités directement au kill** (`EnemyDefinition.XpReward`, `GoldReward`). Une fois le quota de la vague atteint, l'XP n'est plus donnée (gold seul).
- **XP** : `ExperienceCurve` (SO). À chaque niveau : croissance des stats (§5.5) et évolution du passif (§8.1). Niveaux **1 à 15** : **+1 point de compétence** (§7.8). Niveaux **16 à 18** : **stats uniquement**, avec **+1 point de surcharge** dépensable uniquement sur un élément dont la surcharge est débloquée en méta (§7.8, §9.7). Pas de pause ni de choix imposé à la montée de niveau.
- **Niveau max en partie : 18** (`MaxLevel`).
- **Gold** : `Wallet` (valeur, events), dépensé en boutique.

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

Le cas **Argent** permet de **rétrograder** volontairement : un augment de rareté basse peut être très pertinent selon le build (par exemple pour un personnage avec beaucoup de PV), et le joueur doit pouvoir y accéder même quand ses slots sont remplis par des raretés supérieures.

### 9.5 Loot et sorts d'invocateur

- Les ennemis donnent le gold directement. Les **objets au sol** n'arrivent que par des **effets de passif ou d'augment** : `DropLootOnKill(chance, LootTable)`.
- `LootDrop` au sol, ramassé **au contact**. Types : **enclume à stats** (augment **Flambeur**) et **sort d'invocateur** (passif façon **Zoé**).
- **Sort d'invocateur ramassé** (`ConsumableSpell`, usage unique : Flash, Soin, Fantôme…) :
  - il **remplace la compétence propre (F)**, donc le **Flash** du perso ;
  - on **ne peut pas en ramasser un autre tant qu'on n'a pas utilisé celui-ci** ;
  - une fois utilisé, **F redevient la compétence propre** (le Flash), qui reprend son cooldown propre ;
  - implémentation : un `SummonerSlot` avec `Default` (compétence propre) et `Current` ; `PickUp` n'est possible que si `Current == Default`.

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

### 9.7 Méta-progression (rogue-lite)

- **Monnaie persistante** : gagnée **à la fin d'une partie si le stage est clear** (full clear, vagues bonus comprises).
- Elle est dépensée dans des **arbres de compétences accessibles depuis le menu**, composés de nœuds (`MetaNode` : id, prérequis, coût, niveaux, effets). Deux familles :

| Arbre | Rôle |
|---|---|
| **Par élément** (`ElementMetaTree`) | augmente les **dégâts de l'élément** (multiplicateur permanent, appliqué une fois par sort) et **débloque sa surcharge** (§7.8) |
| **Par personnage** (`CharacterMetaTree`) | améliore la **compétence propre**, les **stats** du perso, le **gold de départ**, la possibilité d'avoir **un augment dès le début**, le **passif**, etc. |

- **Effets génériques** (`MetaEffect`), jamais limités à cette liste : bonus de stat, bonus de dégâts d'élément, déblocage de surcharge, gold de départ, augment de départ (avec une rareté), amélioration de la compétence propre, amélioration du passif… Ils sont appliqués au **début de la run** (`RunModifiers`, `StatModifier` avec Source = le nœud méta) ou lus par les systèmes concernés.
- Le **nombre de nœuds et de niveaux** est une donnée à définir plus tard.
- Autres déblocages possibles : items, augments, personnages.
- **`MetaSave`** (JSON) : monnaie et nœuds achetés.
- **`RunSnapshot`** : état complet de la run (niveau, XP, niveaux d'éléments, surcharges, stacks de passifs, items, augments, gold, vies, vague en cours) sérialisé en JSON, pris **au début de chaque vague**. Quitter une vague puis reprendre recharge ce snapshot. Tout état de jeu doit donc être sérialisable.
- **Solo uniquement**, un seul personnage modulable au prototype.

### 9.8 Technique & perf
- **Object Pooling** : `UnityEngine.Pool.ObjectPool<T>` (intégré), via `PrefabPool` (`Systems.Pooling`, service du `GameLifetimeScope` : un pool par prefab, instances créées par le conteneur). Utilisé par les projectiles et les VFX de sorts.
- **Caméra** : `CameraController` maison, verrouillée / libre façon LoL (§4.8). Cinemachine reste une option si le besoin grossit.
- **Détection de cibles** : `Physics.OverlapSphereNonAlloc` ou grille spatiale ; éviter `FindObjectsOfType` ; mettre en cache.
- **Pas de `GetComponent` en boucle** : références mises en cache à l'`Init`.
- **Éviter l'allocation** : listes réutilisées, `struct` pour `DamageInfo`.
- **Animations** : privilégier flipbook/sprite swap simple pour les ennemis en masse plutôt qu'un Animator par entité.
- **UI** : HUD (PV, XP, timer de vague, quota, vies, icônes d'éléments et de sorts), écrans de boutique / enclumes / augments, choix d'éléments, damage numbers (TextMeshPro poolé ou rendu par particules).
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
│   │   ├── Spells/          (ElementDefinition, SpellDefinition, SpellBook, SpellResolver, Spell, ElementCaster, ManaPool, CastContext, implémentations/)
│   │   ├── Input/           (PlayerCommandInput, SpellIndicator, ClickMarker)
│   │   ├── AI/              (EnemyAI, EnemyDefinition, BossDefinition, PatternRunner, FlowField)
│   │   ├── Progression/     (ExperienceCurve, StatGrowth)
│   │   ├── Meta/            (MetaNode, MetaEffect, ElementMetaTree, CharacterMetaTree, MetaSave, RunModifiers)
│   │   ├── Items/           (ItemDefinition, AnvilDefinition, StatChoiceDefinition, ElixirDefinition, Inventory, Wallet, Shop)
│   │   ├── Loot/            (LootDrop, LootTable, ConsumableSpell)
│   │   ├── Augments/        (AugmentDefinition, AugmentController, AugmentDraft)
│   │   ├── Passives/        (PassiveDefinition, Passive, StackCounter, CharacterEvents, CapabilityController)
│   │   ├── Run/             (RunDirector, WaveDefinition, EnemySpawner, Intermission)
│   │   └── UI/
│   ├── Data/                (Characters, Enemies, Elements, Spells, SpellBooks, Passives, Items, Augments, Waves, XPCurve)
│   ├── Prefabs/
│   ├── Art/
│   └── Scenes/
```
Utiliser des **Assembly Definitions** (`.asmdef`) par dossier majeur pour accélérer la compilation.

---

## 11. Plan de réalisation (ordre conseillé)

| Phase | Contenu | Résultat jouable |
|---|---|---|
| **0** ✅ | **Fondations** (§14) : dépôt Git, arborescence et asmdef, `IEventBus`, `GameState` (+ `Win`), `RootLifetimeScope`, SO de réglages, tests EditMode + PlayMode. *Reste : `RunStateMachine` (avec la phase 7).* | Projet propre qui compile, états de jeu et événements en place |
| **1** ✅ | `CharacterMovement` (clic droit, **NavMesh**), marqueur, caméra verrouillée / libre (§4.8), `TestScene`, prefab 2.5D (`Character` + variant `Player`, sprite billboard, §3) | Un perso qui se déplace au clic en évitant les obstacles |
| **2** ✅ | `Stat` / `StatModifier` / `StatController` + toutes les stats + tests unitaires | Stats modifiables |
| **3** ✅ | `CharacterDefinition`, `Character.Init`, `BasicAttackDefinition`, `CharacterLevel` (XP, points de compétence, croissance), vitesse lue depuis `MoveSpeed`. Sprite et taille du corps depuis la définition. | Un perso configurable par données |
| **4** 🟡 | `Health`, `DamageInfo`, formules, régénération des PV, barres de PV, chiffres de dégâts ✅. *Reste : bouclier, CC et ténacité.* | Dégâts corrects |
| **5** 🟡 | Clic droit sur ennemi, `BasicAttacker`, projectiles poolés, Q + clic gauche (attack-move) ✅, dummies d'entraînement (XP au kill). *Reste : `CommandController`.* | Attaque de base façon LoL |
| **5b** ✅ | `ManaPool`, `ElementSelection` (A/Z/E, T reset), `SpellBook` + `SpellResolver`, coût en mana + UI, déblocage d'éléments par points (niv. 1/3/5, Ctrl + A/Z/E) | Éléments activables, coût affiché |
| **5c** ✅ | R + visée + indicateurs, sorts Magic/Physical/Buff, niveaux d'éléments, cooldown par lancement, compétence propre F (Flash) | Sorts lancés à la souris |
| **6** | `EnemyDefinition`, `EnemyAI` (flow field + NavMesh pour élites), mana TFT + sort spécial, spawner, XP/gold au kill | Un combat complet avec des hordes |
| **7** | `RunDirector` (timer + quota, fin de vague = timer ET quota, vies), `Wallet`, `Inventory` (6 slots), `WaveRewardSchedule`, boutiques, enclumes, élixirs, items | Boucle de run + économie |
| **8** | `CharacterEvents`, passifs (`StackCounter`), capacités, augments (4 slots, rerolls), loot | Builds variés |
| **9** | Boss à patterns et items, contenu : sorts, éléments, passifs, items, augments, ennemis | Prototype riche |
| **10** | Méta-progression (arbres par élément et par personnage, monnaie persistante, surcharge), `RunSnapshot` (quitter/reprendre), polish, UI, audio | Prototype solide |

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
- **Quitter/reprendre** : le `RunSnapshot` exige que stacks, modificateurs, items, augments et niveaux d'éléments soient sérialisables ; prévoir ce besoin dès les phases 2 et 3.

---

## 13. Questions ouvertes (à valider avant de coder)

**Résolu** : plan XZ ; reset (T) ; une recette = un sort (19 par perso) ; cooldown par lancement {2, 3.5, 5.5} s ; compétence propre F (Flash 20 s) remplaçable par un sort d'invocateur ; points de compétence aux niveaux 1 à 15 pour monter le niveau des éléments (une fois par sort) ; niveaux 16 à 18 : stats seules, sauf surcharge débloquée en méta ; déblocage des éléments aux niveaux 1 / 3 / 5 ; 3 stacks max par sort ; sorts Magic / Physical / Buff ; passifs à persistance et sources de stacks variables ; XP et gold directs au kill ; fin de vague = timer ET quota ; 3 vies de stage, game over = rejouer le stage depuis la vague 1 ; ordre des fins de vague (augment, enclume prismatique gratuite à 3 choix avec 1 reroll par choix, puis boucle boutique légère / augment / boutique complète) ; vagues bonus : boutique complète après chaque vague et augment toutes les deux vagues ; un élixir max, enclumes à items comptées dans la limite d'un item ; enclumes à stats : une rareté tirée, 3 choix de même rareté ; augments toutes les 3 vagues, reroll doré ~5 % ; méta par arbres d'éléments et de personnages ; quitter/reprendre au début de la vague ; centaines d'ennemis avec creep block ; mana TFT ; boss à patterns avec 3 items ; **pas de crit sur les sorts sauf passif/augment** ; solo, un seul perso de test.

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
- `QuitRequestedEvent` (bouton Quitter du menu, bouton Quit de debug) → `Application.Quit` (arrêt du Play mode dans l'éditeur).

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
- `Time.timeScale` est géré **à un seul endroit** (`TimeScaleService`, abonné aux changements d'état : `Pause` → 0, sinon 1), jamais depuis l'UI.
- L'action map active suit l'état (`InputMapSwitcher`) : `Gameplay` en `Game`, `UI` sinon.
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
| `PointerPositionEvent`, `PointerCommandEvent` (position **écran**), `ConfirmEvent`, `CancelEvent`, `AttackMoveEvent`, `ElementKeyEvent` (slot, `LevelUp` si Ctrl), `CastSpellEvent`, `SignatureEvent`, `ResetElementsEvent`, `StopEvent` | `InputReader` | `PlayerCommandInput`, `CameraController`, `QuitOverlay`, plus tard `ElementCaster` |
| `CameraLockEvent`, `ZoomEvent` | `InputReader` | `CameraController` |
| `WaveStartedEvent`, `WaveEndedEvent`, `LifeLostEvent` | `RunDirector` | HUD, audio, `WaveSpawner` |
| `StageClearedEvent`, `StageFailedEvent` | `RunDirector` | `GameFlowController`, méta-progression |
| `EnemyKilledEvent` (xp, gold, position) | `Character` ennemi à sa mort | `Character` joueur (XP) ; plus tard `Wallet`, loot |
| `DamageDealtEvent` (position, montant, type, crit, joueur touché ?) | `Character` touché | `DamageNumbersView` ; plus tard audio, VFX |
| `PlayerHealthChangedEvent`, `PlayerDiedEvent` | `Character` joueur | `PlayerHud` ; plus tard `GameFlowController` (GameOver) |
| `PlayerSpawnedEvent` (transform du joueur) | `PlayerSpawner` | `CameraController` |
| `XpGrantedEvent` | `CharacterDebugPanel` (plus tard : kills) | `Character` joueur |
| `XpChangedEvent`, `LevelUpEvent`, `StatChangedEvent` | `Character` joueur | `PlayerHud`, `CharacterDebugPanel` |
| `PlayerElementsEvent`, `ElementSelectionChangedEvent` (stacks, sort résolu, coût), `ElementLevelsChangedEvent` (niveaux, « + » possibles, points, prochain déblocage) | `ElementCaster` | `PlayerHud` |
| `ElementLevelUpRequestedEvent` | bouton « + » du HUD | `ElementCaster` |
| `GameplayFeedbackEvent` (message) | `ElementCaster` (plus tard : sorts, boutiques…) | `PlayerHud` |
| `PlayerManaChangedEvent` | `Character` joueur | `PlayerHud` |
| `SpellCastEvent` (sort, lancé par le joueur ?) | `SpellExecutor` | plus tard : passifs, audio |
| `AimModeEvent` (None / AttackMove / Spell / Signature) | `PlayerCommandInput`, `SpellTargeting` | les mêmes (une seule visée à la fois) |
| `CooldownStartedEvent` (Elemental / Signature, durée), `SignatureAssignedEvent` | `ElementCaster`, `SignatureAbility` | `PlayerHud` |
| `GoldChangedEvent`, `ItemPurchasedEvent`, `AugmentChosenEvent` | `Wallet`, `Shop`, `AugmentController` | HUD, `PassiveController` |

### 14.4 Injection de dépendances (VContainer)

**Hiérarchie de scopes**

| Scope | Durée de vie | Contenu |
|---|---|---|
| `RootLifetimeScope` | toute l'application (prefab `Assets/Prefabs/RootLifetimeScope.prefab`, référencé par `Assets/Data/Settings/VContainerSettings.asset`, lui-même dans les *Preloaded Assets*) | **En place** : `IEventBus`, `IPanelManager`, `InputReader`, `GameFlowStateMachine`, `GameFlowController`, `InputMapSwitcher`, `TimeScaleService`, `CursorConfinement`, SO `GameFlowSettings`, `CameraSettings` et `ExperienceCurve`. **Plus tard** : `SaveService` / `MetaSave`, catalogues |
| `MenuLifetimeScope` | scène `Menu` | `MainMenuPanel`, `SceneEntryPoint(Menu)` |
| `GameLifetimeScope` | **un stage** (aujourd'hui `TestScene`) | **Root** : `ControlsSettings` (§4.7) et `SpellSettings` (§7.2) en plus. **Game, en place** : la `Camera` principale, `PrefabPool`, `ProjectileLauncher`, `SpellExecutor`, `CameraController`, `QuitOverlay`, `CharacterDebugPanel`, `PlayerHud` et `DamageNumbersView` (UIDocument « GameHud »), `PlayerSpawner` (prefab `Player`, `CharacterDefinition`, point de spawn), `EnemySpawner` (liste `CharacterSpawn` : prefab + définition + point, aujourd'hui les 3 dummies), `SceneEntryPoint(Game)`. Le joueur spawné est injecté par le conteneur (`PlayerCommandInput`, `CharacterVisual`…). **Plus tard** : `RunStateMachine`, `RunDirector`, `WaveSpawner`, `Wallet`, `Inventory`, `Shop`, `AugmentController`, `RunSnapshotService`, le joueur |

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
| Éléments et sorts | `ElementDefinition`, `SpellDefinition`, `SpellBook`, `SpellSettings` (courbe de coût, cooldowns par stacks) |
| Ennemis | `EnemyDefinition`, `BossDefinition`, `PatternDefinition` |
| Run | **`StageDefinition`** (liste de vagues, `WaveRewardSchedule`, `BonusWaveCount`, `StageLives`, difficulté), `WaveDefinition`, `WaveRewardSchedule`, `ExperienceCurve` |
| Économie | `ItemDefinition`, `AnvilDefinition`, `StatChoiceDefinition`, `ElixirDefinition`, `AugmentDefinition`, `LootTable` |
| Méta | `MetaNode`, `ElementMetaTree`, `CharacterMetaTree` |
| Options | **`GameOptions`** (valeurs par défaut : volumes, qualité, sensibilité, bindings par défaut) ; les choix du joueur (`UserOptions`) sont **sauvegardés en JSON** par-dessus |
| Catalogues | `CharacterCatalog`, `ItemCatalog`, `AugmentCatalog`, `StageCatalog` |

**Nommage** : `Char_<Nom>`, `Enemy_<Nom>`, `Elem_<Nom>`, `Spell_<Éléments>_x<stacks>` (ex. `Spell_Fire_x2`), `Item_<Nom>`, `Aug_<Nom>`, `Stage_<Nom>`, `Wave_<Stage>_<N>`.

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
│   │                        InputMapSwitcher, TimeScaleService, CursorConfinement
│   ├── StateMachines/       GameState, GameFlowStateMachine
│   ├── Events/              GameStateChangedEvent, PlayRequested/QuitRequested, StatChangedEvent
│   ├── CameraControl/       CameraController, CameraSettings (SO), Billboard
│   ├── Characters/          Character, CharacterDefinition / EnemyDefinition (SO), CharacterVisual, Faction,
│   │                        PlayerSpawner, EnemySpawner, RespawnOnDeath
│   ├── Progression/         CharacterLevel, StatGrowth (SO), ExperienceCurve (SO)
│   ├── Combat/              BasicAttacker, BasicAttackDefinition (SO), DamageInfo, DamageCalculator, Health, ManaPool, DamageType,
│   │                        Projectile, SkillshotProjectile, ProjectileLauncher, TargetPicker, ITargetSelector
│   ├── Spells/              ElementDefinition, SpellDefinition, SpellBook, SpellSettings (SO), ElementSelection,
│   │                        SpellResolver, ElementLevels, ElementCaster, SpellMath, SpellCaster, SpellExecutor,
│   │                        SpellTargeting, SpellIndicator, SignatureAbility, SpellVfx
│   ├── Movement/            CharacterMovement
│   ├── Controls/            PlayerCommandInput, ClickMarker, RangeIndicator
│   ├── Stats/               Stat, StatModifier, StatController, StatBlock (SO), TimedModifiers…
│   └── UI/                  PlayerHud, ElementBarView, UiStyles, DamageNumbersView, HealthBarView, GameHUD, QuitOverlay,
│                            Panels/MainMenuPanel, DebugTools/CharacterDebugPanel
├── Containers/              Game.Containers — RootLifetimeScope, MenuLifetimeScope, GameLifetimeScope
├── Editor/                  Game.Editor — ProjectSetup, CharacterPrefabSetup, TestSceneSetup, BuildScript
├── Data/                    Settings/ (GameFlowSettings, CameraSettings, ControlsSettings, SpellSettings, VContainerSettings),
│                            Elements/ (Elem_Fire…), Spells/FireElectricityWind/ (19 Spell_…, SpellBook_…), Spells/Signature/Spell_Flash,
│                            Progression/ExperienceCurve, Characters/TestHero/ (Char_TestHero…),
│                            Characters/TrainingDummy/ (Enemy_TrainingDummy : 1000 PV, 30 armure/RM, 150 XP)
├── GameAssets/ActionMaps/   Game.Input — GameActionMaps (.inputactions + C# généré)
├── Prefabs/                 RootLifetimeScope, ClickMarker, Characters/ (Character, Player, TrainingDummy),
│                            Combat/ (Projectile_ArcaneBolt, Projectile_Skillshot, Vfx_SpellShape)
├── GraphicAssets/           Sprites/ (Characters/…, Elements/…, Spells/Spell_Flash, UI/White, VFX/Arcane_Bolt), Materials/Unlit_Sprite
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
- Tests : `Game.Tests.EditMode` (stats, `EventBus`, state machine, disposition clavier, `CharacterLevel`, formules de dégâts, `Health`, `ManaPool`, sélection d'éléments, grimoire et coût (exemples du §7.3), déblocage des éléments, haste, niveaux d'éléments par sort, cône, dégâts de sort, modificateurs temporaires), `Game.Tests.PlayMode` (lancement direct de `TestScene`, Menu → Play → `TestScene`, contournement des obstacles, caméra, init du joueur et montée de niveau via le bus, spawn depuis le prefab, billboard et flip du sprite, attaque d'un dummy, kill → XP → respawn, projectiles poolés, attack-move, cercle de portée, éléments : déblocage → stacks → sort résolu → reset, boule de feu visée sur un dummy (dégâts, mana, cooldown, sélection vidée), buff sur soi qui expire, Flash borné à sa portée puis en cooldown).

### 14.9 Actions d'entrée à prévoir (Input System)

Map **Gameplay** (les actions `Move`, `Look`, `Jump`… du template ne sont plus utilisées, à nettoyer) :

| Action | Type | Binding par défaut |
|---|---|---|
| `PointerPosition` | Value (Vector2) | souris |
| `PointerCommand` | Button | clic droit (déplacement ou attaque) |
| `Confirm` | Button | clic gauche (valide une visée) |
| `Cancel` | Button | Échap (annule la visée, sinon ouvre la pause) |
| `AttackMove` | Button | Q (maintenu ; `AttackMoveEvent.Pressed` vrai à l'appui, faux au relâchement) |
| `Element1`, `Element2`, `Element3` | Button | A, Z, E |
| `CastSpell` | Button | R |
| `Signature` | Button | F |
| `ResetElements` | Button | T |
| `Stop` | Button | S |
| `LevelUpModifier` | Button | Ctrl gauche / droit (maintenu avec A/Z/E : dépense un point) |
| `CameraLock` | Button | Espace, clic molette |
| `Zoom` | Value (Vector2) | molette |

Les lettres sont remappées selon `ControlsSettings.KeyboardLayout` (§4.7). Prévoir le **rebind** (`UserOptions`).
