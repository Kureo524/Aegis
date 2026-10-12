#if UNITY_EDITOR
using System.IO;
using Features.Characters;
using Features.Controls;
using Features.Movement;
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
    /// Enemies will be another variant (+ AI). What differs between two characters of the same
    /// kind (sprite, size, stats) stays in their CharacterDefinition, never in a variant.
    /// </summary>
    public static class CharacterPrefabSetup {
        public const string BasePrefabPath = "Assets/Prefabs/Characters/Character.prefab";
        public const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        public const string PlaceholderSpritePath = "Assets/GraphicAssets/Sprites/Characters/Placeholder_Hero.png";
        private const string ClickMarkerPath = "Assets/Prefabs/ClickMarker.prefab";

        private const int SpriteWidth = 64;
        private const int SpriteHeight = 128;
        private const int PixelsPerUnit = 64;   // 2 world units tall

        public static Character CreatePlayerPrefab() {
            CreateBasePrefab();
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
            return AssetDatabase.LoadAssetAtPath<Character>(PlayerPrefabPath);
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
