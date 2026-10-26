#if UNITY_EDITOR
using System.IO;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Controls;
using Features.Enemies;
using Features.Movement;
using Features.Spells;
using Features.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace EditorTools {
    /// <summary>
    /// Generates the character prefabs (design doc §2, §3) and a placeholder sprite. Called by
    /// <see cref="ProjectSetup"/>; each asset is only created when missing, never overwritten.
    ///
    ///   Character.prefab   generic base: physics root (Rigidbody, BoxCollider, NavMeshAgent,
    ///                      CharacterMovement, Character) + "Visual" child (billboard sprite)
    ///   Player.prefab      variant of Character + PlayerCommandInput
    ///   TrainingDummy.prefab  variant of Character + RespawnOnDeath
    ///   Enemy.prefab       variant of Character + EnemyAI (discrete collisions: hundreds of them) What differs between two characters of the same
    /// kind (sprite, size, stats) stays in their CharacterDefinition, never in a variant.
    /// </summary>
    public static class CharacterPrefabSetup {
        public const string BasePrefabPath = "Assets/Prefabs/Characters/Character.prefab";
        public const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        public const string DummyPrefabPath = "Assets/Prefabs/Characters/TrainingDummy.prefab";
        public const string EnemyPrefabPath = "Assets/Prefabs/Characters/Enemy.prefab";
        public const string PlaceholderSpritePath = "Assets/GraphicAssets/Sprites/Characters/Placeholder_Hero.png";
        public const string WhiteSpritePath = "Assets/GraphicAssets/Sprites/UI/White.png";
        private const string StatusIconsDir = "Assets/GraphicAssets/Sprites/Status";
        public const string ArcaneBoltPrefabPath = "Assets/Prefabs/Combat/Projectile_ArcaneBolt.prefab";
        public const string SkillshotPrefabPath = "Assets/Prefabs/Combat/Projectile_Skillshot.prefab";
        public const string ShapeVfxPrefabPath = "Assets/Prefabs/Combat/Vfx_SpellShape.prefab";
        private const string ArcaneBoltSpritePath = "Assets/GraphicAssets/Sprites/VFX/Arcane_Bolt.png";
        private const string UnlitMaterialPath = "Assets/GraphicAssets/Materials/Unlit_Sprite.mat";
        private const string ClickMarkerPath = "Assets/Prefabs/ClickMarker.prefab";

        private const int SpriteWidth = 64;
        private const int SpriteHeight = 128;
        private const int PixelsPerUnit = 64;   // 2 world units tall

        public static Character CreatePlayerPrefab() {
            CreateBasePrefab();
            UpgradeBasePrefab();
            if (!File.Exists(PlayerPrefabPath)) {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                instance.name = "Player";

                var input = instance.AddComponent<PlayerCommandInput>();
                input.characterMovement = instance.GetComponent<CharacterMovement>();
                input.clickMarkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClickMarkerPath);

                // Saving an instance of a prefab creates a variant of it
                PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
                Object.DestroyImmediate(instance);
            }

            EditPrefab(PlayerPrefabPath, root => {
                var input = root.GetComponent<PlayerCommandInput>();
                input.character = root.GetComponent<Character>();
                input.attacker = root.GetComponent<BasicAttacker>();

                // The element caster and the F-only signature ability are gone (champion abilities, §7)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

                RangeIndicator indicator = root.GetComponentInChildren<RangeIndicator>(true);
                if (indicator == null) indicator = CreateRangeIndicator(root.transform);
                input.rangeIndicator = indicator;

                var character = root.GetComponent<Character>();
                var spellCaster = root.GetComponent<SpellCaster>();
                input.spellCaster = spellCaster;

                SpellIndicator shape = root.GetComponentInChildren<SpellIndicator>(true);
                if (shape == null) shape = CreateSpellIndicator(root.transform);

                if (!root.TryGetComponent(out SpellTargeting targeting)) targeting = root.AddComponent<SpellTargeting>();
                SetRefs(targeting, ("owner", character), ("indicator", shape), ("rangeIndicator", indicator));
                if (!root.TryGetComponent(out AbilityCaster abilities)) abilities = root.AddComponent<AbilityCaster>();
                SetRefs(abilities, ("owner", character), ("targeting", targeting), ("caster", spellCaster));
            });
            return AssetDatabase.LoadAssetAtPath<Character>(PlayerPrefabPath);
        }

        public static Character CreateDummyPrefab() {
            CreatePlayerPrefab();   // makes sure the base exists and is up to date
            if (!File.Exists(DummyPrefabPath)) {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                instance.name = "TrainingDummy";
                var respawn = instance.AddComponent<RespawnOnDeath>();
                var so = new SerializedObject(respawn);
                so.FindProperty("character").objectReferenceValue = instance.GetComponent<Character>();
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(instance, DummyPrefabPath);
                Object.DestroyImmediate(instance);
            }
            return AssetDatabase.LoadAssetAtPath<Character>(DummyPrefabPath);
        }

        public static Character CreateEnemyPrefab() {
            CreatePlayerPrefab();   // makes sure the base exists and is up to date
            if (!File.Exists(EnemyPrefabPath)) {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                instance.name = "Enemy";
                instance.AddComponent<EnemyAI>();
                PrefabUtility.SaveAsPrefabAsset(instance, EnemyPrefabPath);
                Object.DestroyImmediate(instance);
            }
            EditPrefab(EnemyPrefabPath, root => {
                // Continuous detection on hundreds of bodies costs a lot; enemies are slow enough without it
                root.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.Discrete;
                if (!root.TryGetComponent(out EnemyAI ai)) ai = root.AddComponent<EnemyAI>();
                SetRefs(ai, ("owner", root.GetComponent<Character>()), ("movement", root.GetComponent<CharacterMovement>()),
                        ("attacker", root.GetComponent<BasicAttacker>()), ("caster", root.GetComponent<SpellCaster>()),
                        ("agent", root.GetComponent<NavMeshAgent>()));
            });
            return AssetDatabase.LoadAssetAtPath<Character>(EnemyPrefabPath);
        }

        /// <summary>Character sprite import settings: pixel art, 64 px per unit, pivot at the feet. Idempotent.</summary>
        public static void ConfigureCharacterSprite(string path) {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) && importer.filterMode == FilterMode.Point
                && settings.spriteAlignment == (int)SpriteAlignment.BottomCenter) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // Adds what later phases put on every character (basic attack, HP bar). Idempotent, keeps edits.
        private static void UpgradeBasePrefab() {
            EditPrefab(BasePrefabPath, root => {
                var character = root.GetComponent<Character>();
                var movement = root.GetComponent<CharacterMovement>();
                var visual = root.GetComponentInChildren<CharacterVisual>(true);

                if (!root.TryGetComponent(out BasicAttacker attacker)) attacker = root.AddComponent<BasicAttacker>();
                var attackerSo = new SerializedObject(attacker);
                attackerSo.FindProperty("owner").objectReferenceValue = character;
                attackerSo.FindProperty("movement").objectReferenceValue = movement;
                attackerSo.FindProperty("visual").objectReferenceValue = visual;
                attackerSo.ApplyModifiedPropertiesWithoutUndo();

                HealthBarView bar = root.GetComponentInChildren<HealthBarView>(true);
                if (bar == null) bar = CreateHealthBar(root.transform);
                AddStatusIcon(bar);

                if (!root.TryGetComponent(out SpellCaster spellCaster)) spellCaster = root.AddComponent<SpellCaster>();
                var casterSo = new SerializedObject(spellCaster);
                casterSo.FindProperty("owner").objectReferenceValue = character;
                casterSo.FindProperty("movement").objectReferenceValue = movement;
                casterSo.FindProperty("attacker").objectReferenceValue = attacker;
                casterSo.FindProperty("visual").objectReferenceValue = visual;
                casterSo.ApplyModifiedPropertiesWithoutUndo();

                var characterSo = new SerializedObject(character);
                characterSo.FindProperty("attacker").objectReferenceValue = attacker;
                characterSo.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void SetRefs(Object target, params (string field, Object value)[] refs) {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SpellIndicator CreateSpellIndicator(Transform parent) {
            var go = new GameObject("SpellIndicator");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = UnlitMaterial();
            line.widthMultiplier = 0.08f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 51;
            var indicator = go.AddComponent<SpellIndicator>();
            go.SetActive(false);
            return indicator;
        }

        /// <summary>Straight spell projectile (skillshots): same look as the arcane bolt, tinted per spell.</summary>
        public static SkillshotProjectile CreateSkillshotPrefab() {
            if (!File.Exists(SkillshotPrefabPath)) {
                GameObject root = BuildBoltVisuals("Projectile_Skillshot", out SpriteRenderer sprite, out TrailRenderer trail);
                trail.widthCurve = AnimationCurve.Linear(0f, 0.35f, 1f, 0f);
                trail.time = 0.18f;
                sprite.transform.localScale = Vector3.one * 1.4f;
                var projectile = root.AddComponent<SkillshotProjectile>();
                SetRefs(projectile, ("sprite", sprite), ("trail", trail));
                PrefabUtility.SaveAsPrefabAsset(root, SkillshotPrefabPath);
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<SkillshotProjectile>(SkillshotPrefabPath);
        }

        /// <summary>Ground shape flash of spells (rings, cones).</summary>
        public static SpellVfx CreateShapeVfxPrefab() {
            if (!File.Exists(ShapeVfxPrefabPath)) {
                Directory.CreateDirectory(Path.GetDirectoryName(ShapeVfxPrefabPath)!);
                var root = new GameObject("Vfx_SpellShape");
                var line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = UnlitMaterial();
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sortingOrder = 52;
                root.AddComponent<SpellVfx>();
                PrefabUtility.SaveAsPrefabAsset(root, ShapeVfxPrefabPath);
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<SpellVfx>(ShapeVfxPrefabPath);
        }

        private static GameObject BuildBoltVisuals(string name, out SpriteRenderer sprite, out TrailRenderer trail) {
            Directory.CreateDirectory(Path.GetDirectoryName(ArcaneBoltPrefabPath)!);
            var root = new GameObject(name);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            sprite = visual.AddComponent<SpriteRenderer>();
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArcaneBoltSpritePath);
            sprite.sortingOrder = 60;
            visual.AddComponent<Billboard>();

            trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = UnlitMaterial();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.22f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sortingOrder = 59;
            return root;
        }

        private static RangeIndicator CreateRangeIndicator(Transform parent) {
            var go = new GameObject("RangeIndicator");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = UnlitMaterial();
            line.widthMultiplier = 0.06f;
            line.startColor = line.endColor = new Color(0.45f, 0.85f, 1f, 0.8f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 50;
            var indicator = go.AddComponent<RangeIndicator>();
            go.SetActive(false);
            return indicator;
        }

        /// <summary>Homing arcane bolt of the test hero's basic attack: glowing orb facing the camera + short trail.</summary>
        public static Projectile CreateArcaneBoltPrefab() {
            if (!File.Exists(ArcaneBoltPrefabPath)) {
                Directory.CreateDirectory(Path.GetDirectoryName(ArcaneBoltPrefabPath)!);
                var root = new GameObject("Projectile_ArcaneBolt");

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var sprite = visual.AddComponent<SpriteRenderer>();
                sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArcaneBoltSpritePath);
                sprite.sortingOrder = 60;
                visual.AddComponent<Billboard>();

                var trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = UnlitMaterial();
                trail.time = 0.12f;
                trail.minVertexDistance = 0.05f;
                trail.widthCurve = AnimationCurve.Linear(0f, 0.22f, 1f, 0f);
                trail.colorGradient = new Gradient {
                    colorKeys = new[] { new GradientColorKey(new Color(0.55f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0.5f, 1f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
                };
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.sortingOrder = 59;

                var projectile = root.AddComponent<Projectile>();
                var so = new SerializedObject(projectile);
                so.FindProperty("trail").objectReferenceValue = trail;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ArcaneBoltPrefabPath);
                Object.DestroyImmediate(root);
            }
            EditPrefab(ArcaneBoltPrefabPath, root => {
                var projectile = root.GetComponent<Projectile>();
                SetRefs(projectile, ("sprite", root.GetComponentInChildren<SpriteRenderer>(true)));
            });
            return AssetDatabase.LoadAssetAtPath<Projectile>(ArcaneBoltPrefabPath);
        }

        // Unlit, vertex-colored, alpha-blended: for lines, trails and small VFX.
        private static Material UnlitMaterial() {
            var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (material != null) return material;
            Directory.CreateDirectory(Path.GetDirectoryName(UnlitMaterialPath)!);
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, UnlitMaterialPath);
            return material;
        }

        // Phase 9b: status icon left of the bar (added to existing prefabs too)
        private static void AddStatusIcon(HealthBarView bar) {
            var so = new SerializedObject(bar);
            if (so.FindProperty("statusIcon").objectReferenceValue == null) {
                var go = new GameObject("StatusIcon");
                go.transform.SetParent(bar.transform, false);
                go.transform.localPosition = new Vector3(-0.68f, 0f, -0.002f);
                var icon = go.AddComponent<SpriteRenderer>();
                icon.sortingOrder = 102;
                icon.enabled = false;
                so.FindProperty("statusIcon").objectReferenceValue = icon;
            }
            string[] names = System.Enum.GetNames(typeof(Features.Combat.StatusKind));
            SerializedProperty sprites = so.FindProperty("statusSprites");
            sprites.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) {
                string path = $"{StatusIconsDir}/Status_{names[i]}.png";
                EconomySetup.ConfigureIcon(path);
                sprites.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HealthBarView CreateHealthBar(Transform parent) {
            Sprite white = CreateWhiteSprite();
            var barGo = new GameObject("HealthBar");
            barGo.transform.SetParent(parent, false);
            barGo.transform.localPosition = new Vector3(0f, 2.15f, 0f);

            SpriteRenderer Part(string name, Color color, Vector3 scale, int order) {
                var go = new GameObject(name);
                go.transform.SetParent(barGo.transform, false);
                go.transform.localScale = scale;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = white;
                r.color = color;
                r.sortingOrder = order;   // drawn over character sprites
                return r;
            }

            var background = Part("Background", new Color(0.05f, 0.05f, 0.08f, 0.85f), new Vector3(1.06f, 0.16f, 1f), 100);
            var fill = Part("Fill", Color.green, new Vector3(1f, 0.1f, 1f), 101);

            var bar = barGo.AddComponent<HealthBarView>();
            var so = new SerializedObject(bar);
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bar;
        }

        private static Sprite CreateWhiteSprite() {
            if (!File.Exists(WhiteSpritePath)) {
                Directory.CreateDirectory(Path.GetDirectoryName(WhiteSpritePath)!);
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                File.WriteAllBytes(WhiteSpritePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(WhiteSpritePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 4;   // 1 world unit
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit) {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateBasePrefab() {
            if (File.Exists(BasePrefabPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(BasePrefabPath)!);

            var root = new GameObject("Character");

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var body = root.AddComponent<BoxCollider>();
            body.size = new Vector3(0.8f, 1.8f, 0.8f);
            body.center = new Vector3(0f, 0.9f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.baseOffset = 0f;
            agent.radius = 0.4f;
            agent.height = 1.8f;

            var movement = root.AddComponent<CharacterMovement>();
            var character = root.AddComponent<Character>();

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);
            var spriteRenderer = visualGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreatePlaceholderSprite();   // editor preview; Init applies the definition's
            var visual = visualGo.AddComponent<CharacterVisual>();

            var visualSo = new SerializedObject(visual);
            visualSo.FindProperty("movement").objectReferenceValue = movement;
            visualSo.ApplyModifiedPropertiesWithoutUndo();

            var characterSo = new SerializedObject(character);
            characterSo.FindProperty("movement").objectReferenceValue = movement;
            characterSo.FindProperty("visual").objectReferenceValue = visual;
            characterSo.FindProperty("body").objectReferenceValue = body;
            characterSo.FindProperty("agent").objectReferenceValue = agent;
            characterSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>Simple pixel-art figure facing right (eye and sword on the right side), feet at the bottom.</summary>
        public static Sprite CreatePlaceholderSprite() {
            if (!File.Exists(PlaceholderSpritePath)) {
                Directory.CreateDirectory(Path.GetDirectoryName(PlaceholderSpritePath)!);
                File.WriteAllBytes(PlaceholderSpritePath, DrawPlaceholder().EncodeToPNG());
                AssetDatabase.ImportAsset(PlaceholderSpritePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        }

        private static Texture2D DrawPlaceholder() {
            var pixels = new Color32[SpriteWidth * SpriteHeight];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            void Rect(int x0, int y0, int x1, int y1, Color32 c) {
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) pixels[y * SpriteWidth + x] = c;
            }
            void Disc(int cx, int cy, int r, Color32 c) {
                for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) pixels[y * SpriteWidth + x] = c;
            }

            var boots = new Color32(60, 45, 40, 255);
            var legs = new Color32(50, 60, 90, 255);
            var tunic = new Color32(60, 110, 200, 255);
            var belt = new Color32(120, 80, 40, 255);
            var skin = new Color32(240, 200, 160, 255);
            var hair = new Color32(90, 55, 30, 255);
            var steel = new Color32(200, 205, 215, 255);
            var eye = new Color32(20, 20, 30, 255);

            Rect(20, 2, 30, 10, boots);  Rect(34, 2, 44, 10, boots);    // boots
            Rect(22, 10, 30, 34, legs);  Rect(34, 10, 42, 34, legs);    // legs
            Rect(18, 34, 46, 80, tunic);                               // tunic
            Rect(18, 48, 46, 53, belt);                                // belt
            Rect(12, 52, 18, 76, tunic); Rect(46, 56, 54, 64, skin);   // back arm, front hand
            Rect(54, 44, 57, 100, steel); Rect(50, 62, 61, 65, belt);  // sword + guard
            Disc(32, 96, 15, skin);                                    // head
            Rect(17, 104, 47, 112, hair); Rect(17, 96, 24, 106, hair); // hair, back of the head
            Rect(38, 94, 42, 99, eye);                                 // eye on the right: faces right

            // 1 px dark outline around the silhouette
            var outline = new Color32(25, 25, 35, 255);
            var outlined = (Color32[])pixels.Clone();
            for (int y = 0; y < SpriteHeight; y++)
            for (int x = 0; x < SpriteWidth; x++) {
                if (pixels[y * SpriteWidth + x].a != 0) continue;
                bool touches = false;
                for (int dy = -1; dy <= 1 && !touches; dy++)
                for (int dx = -1; dx <= 1 && !touches; dx++) {
                    int nx = x + dx, ny = y + dy;
                    touches = nx >= 0 && ny >= 0 && nx < SpriteWidth && ny < SpriteHeight && pixels[ny * SpriteWidth + nx].a != 0;
                }
                if (touches) outlined[y * SpriteWidth + x] = outline;
            }

            var texture = new Texture2D(SpriteWidth, SpriteHeight, TextureFormat.RGBA32, false);
            texture.SetPixels32(outlined);
            texture.Apply();
            return texture;
        }
    }
}
#endif
