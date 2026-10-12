#if UNITY_EDITOR
using System.IO;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Controls;
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
    /// Enemies will be another variant (+ AI). What differs between two characters of the same
    /// kind (sprite, size, stats) stays in their CharacterDefinition, never in a variant.
    /// </summary>
    public static class CharacterPrefabSetup {
        public const string BasePrefabPath = "Assets/Prefabs/Characters/Character.prefab";
        public const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        public const string DummyPrefabPath = "Assets/Prefabs/Characters/TrainingDummy.prefab";
        public const string PlaceholderSpritePath = "Assets/GraphicAssets/Sprites/Characters/Placeholder_Hero.png";
        public const string WhiteSpritePath = "Assets/GraphicAssets/Sprites/UI/White.png";
        public const string ArcaneBoltPrefabPath = "Assets/Prefabs/Combat/Projectile_ArcaneBolt.prefab";
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

                if (!root.TryGetComponent(out ElementCaster caster)) caster = root.AddComponent<ElementCaster>();
                var casterSo = new SerializedObject(caster);
                casterSo.FindProperty("owner").objectReferenceValue = root.GetComponent<Character>();
                casterSo.ApplyModifiedPropertiesWithoutUndo();

                RangeIndicator indicator = root.GetComponentInChildren<RangeIndicator>(true);
                if (indicator == null) indicator = CreateRangeIndicator(root.transform);
                input.rangeIndicator = indicator;
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

                var characterSo = new SerializedObject(character);
                characterSo.FindProperty("attacker").objectReferenceValue = attacker;
                characterSo.FindProperty("healthBar").objectReferenceValue = bar;
                characterSo.ApplyModifiedPropertiesWithoutUndo();
            });
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
