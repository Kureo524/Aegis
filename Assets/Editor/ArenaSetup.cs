#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EditorTools {
    /// <summary>
    /// The arena of the test scene (design doc §9.6), called by <see cref="ProjectSetup"/>: a forest
    /// clearing of radius <see cref="Radius"/> walled by rocks, forest all around, the pathfinding
    /// obstacles dressed as rocks and ruined columns, painted ground, decor, light, fog and
    /// post-processing. Models: Kenney Nature Kit (CC0), recoloured with a natural palette.
    ///
    /// Only the clearing is walkable: the NavMesh is baked from the physics colliders, from a disc
    /// of radius <see cref="Radius"/> (the Ground object) and the obstacles; everything under
    /// "=== Arena ===" is left out of it. An invisible wall around the clearing backs the NavMesh
    /// clamp of CharacterMovement. Built once (fixed seed); Tools > Rebuild Arena rebuilds it.
    /// </summary>
    public static class ArenaSetup {
        public const float Radius = 40f;
        private const string Root = "=== Arena ===";
        private const string LandmarksRoot = "=== Landmarks ===";

        // Rock islands between the center and the edge: cover to kite around (they carve the NavMesh)
        private static readonly Vector3[] Landmarks = {
            new(-24f, 0f, -18f), new(1f, 0f, -27f), new(25f, 0f, -19f), new(-29f, 0f, 9f), new(-11f, 0f, 29f), new(29f, 0f, 22f),
        };
        private static readonly Vector3[] Campfires = { new(-34f, 0f, -12f), new(33f, 0f, 2f) };
        private const string Kenney = "Assets/GraphicAssets/Environment/Kenney/Nature";
        private const string Materials = "Assets/GraphicAssets/Environment/Materials";
        private const string GroundDir = "Assets/GraphicAssets/Environment/Ground";
        private const string FloorMeshPath = "Assets/GraphicAssets/Environment/ArenaFloor.asset";
        private const string VolumePath = "Assets/Settings/ArenaVolumeProfile.asset";

        // Kenney's teal palette → a natural one (sRGB)
        private static readonly Dictionary<string, Color> Palette = new() {
            { "grass", new Color(0.31f, 0.47f, 0.22f) },
            { "leafsGreen", new Color(0.28f, 0.47f, 0.22f) },
            { "leafsDark", new Color(0.15f, 0.32f, 0.21f) },
            { "dirt", new Color(0.42f, 0.33f, 0.24f) },
            { "stone", new Color(0.56f, 0.57f, 0.58f) },
            { "stoneDark", new Color(0.40f, 0.41f, 0.45f) },
            { "woodBark", new Color(0.38f, 0.26f, 0.17f) },
            { "woodBarkDark", new Color(0.28f, 0.20f, 0.14f) },
            { "wood", new Color(0.52f, 0.36f, 0.22f) },
            { "woodDark", new Color(0.40f, 0.27f, 0.17f) },
            { "woodInner", new Color(0.78f, 0.66f, 0.50f) },
            { "colorRed", new Color(0.80f, 0.22f, 0.20f) },
            { "colorYellow", new Color(0.95f, 0.76f, 0.26f) },
            { "colorPurple", new Color(0.60f, 0.46f, 0.90f) },
            { "colorTan", new Color(0.85f, 0.66f, 0.46f) },
            { "colorWhite", new Color(0.92f, 0.90f, 0.85f) },
            { "_defaultMat", new Color(0.50f, 0.50f, 0.50f) },
        };

        private static readonly string[] BoundaryRocks = {
            "rock_tallA", "rock_tallB", "rock_tallC", "rock_tallD", "rock_tallF", "rock_tallG",
            "rock_tallH", "rock_tallI", "stone_tallA", "stone_tallC", "stone_tallE", "rock_largeD", "rock_largeF",
        };
        private static readonly string[] WallRocks = {
            "rock_tallA", "rock_tallB", "rock_tallC", "rock_tallG", "rock_tallH", "stone_tallA", "stone_tallB",
            "stone_tallD", "rock_largeB", "rock_largeD", "rock_largeF",
        };
        private static readonly string[] Trees = {
            "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_pineTallD", "tree_pineDefaultA", "tree_pineDefaultB",
            "tree_pineRoundA", "tree_pineRoundC", "tree_pineRoundE", "tree_oak_dark", "tree_default_dark", "tree_detailed_dark",
            "tree_fat_darkh", "tree_oak", "tree_detailed",
        };
        private static readonly string[] Grass = { "grass", "grass_large", "grass_leafs", "grass_leafsLarge" };
        private static readonly string[] Flowers = {
            "flower_redA", "flower_redB", "flower_redC", "flower_yellowA", "flower_yellowB", "flower_yellowC",
            "flower_purpleA", "flower_purpleB", "flower_purpleC",
        };
        private static readonly string[] Bushes = { "plant_bush", "plant_bushLarge", "plant_bushSmall", "plant_bushDetailed" };
        private static readonly string[] Pebbles = {
            "rock_smallA", "rock_smallB", "rock_smallC", "rock_smallD", "rock_smallE", "stone_smallA", "stone_smallB",
            "stone_smallC", "stone_smallD", "stone_smallE",
        };
        private static readonly string[] Forest = { "mushroom_redGroup", "mushroom_tanGroup", "stump_old", "stump_round", "log", "log_large" };

        private static System.Random _rng;
        private static Dictionary<string, Material> _materials;

        /// <summary>Builds the arena once (an existing one is kept, so the scene file does not churn).</summary>
        public static void Run() => Build(force: false);

        /// <summary>Rebuilds the arena from scratch (after changing this script).</summary>
        [MenuItem("Tools/Rebuild Arena")]
        public static void Rebuild() => Build(force: true);

        private static void Build(bool force) {
            ImportModels();
            var scene = EditorSceneManager.OpenScene(ProjectSetup.TestScenePath, OpenSceneMode.Single);
            if (!force && GameObject.Find(Root) != null && GameObject.Find(LandmarksRoot) != null) return;
            _rng = new System.Random(1337);

            foreach (string name in new[] { Root, LandmarksRoot }) {
                GameObject old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }
            var root = new GameObject(Root).transform;
            var ignore = root.gameObject.AddComponent<NavMeshModifier>();
            ignore.ignoreFromBuild = true;   // and its children: only the clearing is walkable

            Floor();
            Ground(root);
            Boundary(root);
            ForestRing(root);
            Ruins(root);
            Islands();
            Decor(root);
            Fires(root);
            Lighting();
            PostProcessing();

            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface != null) {
                surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                TestSceneSetup.BakeNavMesh(surface);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- models and materials

        private static void ImportModels() {
            Directory.CreateDirectory(Materials);
            _materials = new Dictionary<string, Material>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (KeyValuePair<string, Color> kv in Palette) {
                string path = $"{Materials}/Nature_{kv.Key.TrimStart('_')}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) {
                    m = new Material(lit);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.SetColor("_BaseColor", kv.Value);
                m.SetFloat("_Smoothness", 0.08f);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                _materials[kv.Key] = m;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Kenney })) {
                var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                bool changed = importer.importAnimation || importer.importCameras || importer.importLights || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                var existing = importer.GetExternalObjectMap();
                foreach (KeyValuePair<string, Material> kv in _materials) {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                    if (!existing.TryGetValue(id, out Object current) || current != kv.Value) {
                        importer.AddRemap(id, kv.Value);
                        changed = true;
                    }
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        private static GameObject Place(string model, Transform parent, Vector3 position, float yaw, Vector3 scale, bool shadows = true) {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kenney}/{model}.fbx");
            if (asset == null) {
                Debug.LogWarning($"[ArenaSetup] Missing model {model}");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = scale;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        private static T Pick<T>(T[] items) => items[_rng.Next(items.Length)];
        private static Vector3 OnRing(float radius, float angle) => new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

        // ---------------------------------------------------------------- ground

        // The walkable disc: Ground keeps its NavMeshSurface, its mesh becomes the clearing
        private static void Floor() {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FloorMeshPath);
            if (mesh == null) {
                mesh = Disc(Radius, 96);
                AssetDatabase.CreateAsset(mesh, FloorMeshPath);
            }
            GameObject ground = GameObject.Find("Ground");
            ground.transform.localScale = Vector3.one;
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            var col = ground.GetComponent<MeshCollider>();
            if (col != null) col.sharedMesh = mesh;
            ground.GetComponent<MeshRenderer>().enabled = false;   // drawn by the painted ground
        }

        private static Mesh Disc(float radius, int segments) {
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++) {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "ArenaFloor", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GroundMaterial(string name, string baseMap, float tiling, float detailTiling) {
            string path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            SetTextureImport($"{GroundDir}/{baseMap}.png", true);
            SetTextureImport($"{GroundDir}/Ground_Detail.png", true, linear: true);   // ×2 detail: 0.5 grey = neutral
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundDir}/{baseMap}.png"));
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0f);
            m.SetTexture("_DetailAlbedoMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundDir}/Ground_Detail.png"));
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(detailTiling, detailTiling));
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void SetTextureImport(string path, bool repeat, bool linear = false) {
            if (AssetImporter.GetAtPath(path) is not TextureImporter t) return;
            TextureWrapMode wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (t.wrapMode == wrap && t.anisoLevel == 8 && t.maxTextureSize == 2048 && t.sRGBTexture == !linear) return;
            t.sRGBTexture = !linear;
            t.wrapMode = wrap;
            t.anisoLevel = 8;
            t.maxTextureSize = 2048;
            t.SaveAndReimport();
        }

        private static void Ground(Transform root) {
            Material painted = GroundMaterial("Ground_Arena", "Ground_Arena", 1f, 45f);
            Material forest = GroundMaterial("Ground_Forest", "Ground_Forest", 34f, 180f);
            SetTextureImport($"{GroundDir}/Ground_Arena.png", false);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Ground_Painted";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.SetPositionAndRotation(new Vector3(0f, 0.005f, 0f), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(100f, 100f, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = painted;
            quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Forest floor all around, far enough for the most zoomed-out camera; also a physics floor
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground_Forest";
            plane.transform.SetParent(root, false);
            plane.transform.position = new Vector3(0f, -0.01f, 0f);
            plane.transform.localScale = new Vector3(40f, 1f, 40f);
            plane.GetComponent<MeshRenderer>().sharedMaterial = forest;
            plane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- edge of the clearing

        private static void Boundary(Transform root) {
            var parent = new GameObject("Boundary").transform;
            parent.SetParent(root, false);
            // Two rows of big rocks right outside the walkable disc
            for (int row = 0; row < 2; row++) {
                float radius = Radius + 2.2f + row * 3.4f;
                int count = Mathf.RoundToInt(2f * Mathf.PI * radius / (row == 0 ? 2.3f : 3.2f));
                for (int i = 0; i < count; i++) {
                    float a = (i + Range(-0.3f, 0.3f)) * Mathf.PI * 2f / count;
                    float s = row == 0 ? Range(2.6f, 3.6f) : Range(3.4f, 4.8f);
                    Vector3 p = OnRing(radius + Range(-0.6f, 0.8f), a);
                    Place(Pick(BoundaryRocks), parent, p, Range(0f, 360f), new Vector3(s * Range(0.9f, 1.3f), s * Range(0.9f, 1.6f), s * Range(0.9f, 1.3f)));
                }
            }
            // Invisible wall (physics): nothing is pushed through, whatever the NavMesh says
            var walls = new GameObject("InvisibleWall").transform;
            walls.SetParent(root, false);
            const int segments = 48;
            float side = 2f * (Radius + 1.1f) * Mathf.Tan(Mathf.PI / segments) + 0.3f;
            for (int i = 0; i < segments; i++) {
                float a = i * Mathf.PI * 2f / segments;
                var w = new GameObject($"Wall_{i:00}");
                w.transform.SetParent(walls, false);
                w.transform.position = OnRing(Radius + 1.1f, a) + Vector3.up * 2f;
                w.transform.rotation = Quaternion.LookRotation(-OnRing(1f, a));
                w.AddComponent<BoxCollider>().size = new Vector3(side, 4f, 1f);
            }
        }

        private static void ForestRing(Transform root) {
            var parent = new GameObject("Forest").transform;
            parent.SetParent(root, false);
            // Rejection-sampled trees between the rocks and the far edge, denser near the clearing
            var placed = new List<Vector3>();
            for (int attempt = 0; attempt < 9000 && placed.Count < 1500; attempt++) {
                float a = Range(0f, Mathf.PI * 2f);
                float r = Mathf.Lerp(Radius + 7f, 100f, Mathf.Pow(Range(0f, 1f), 1.4f));
                Vector3 p = OnRing(r, a);
                float spacing = r < 60f ? 3.2f : 4.2f;
                bool free = true;
                foreach (Vector3 q in placed)
                    if ((q - p).sqrMagnitude < spacing * spacing) { free = false; break; }
                if (!free) continue;
                placed.Add(p);
                float s = Range(4.2f, 6.4f);
                Place(Pick(Trees), parent, p, Range(0f, 360f), new Vector3(s, s * Range(0.9f, 1.25f), s), r < 75f);
            }
            for (int i = 0; i < 160; i++) {
                Vector3 p = OnRing(Range(Radius + 6f, 70f), Range(0f, Mathf.PI * 2f));
                float s = Range(2.2f, 3.4f);
                Place(Pick(_rng.NextDouble() < 0.5 ? Bushes : Forest), parent, p, Range(0f, 360f), Vector3.one * s, false);
            }
        }

        // ---------------------------------------------------------------- obstacles

        // The pathfinding obstacles keep their box colliders (NavMesh holes, physics), hidden under
        // rocks (walls) and ruined columns (pillars)
        private static void Ruins(Transform root) {
            var parent = new GameObject("Ruins").transform;
            parent.SetParent(root, false);
            foreach (var (name, position, size) in TestSceneSetup.Obstacles) {
                GameObject box = GameObject.Find(name);
                if (box != null && box.TryGetComponent(out MeshRenderer r)) r.enabled = false;
                if (name.StartsWith("Pillar")) {
                    float s = size.x / 0.3f * 0.85f;
                    Place(_rng.NextDouble() < 0.35 ? "statue_columnDamaged" : "statue_column", parent, new Vector3(position.x, 0f, position.z),
                          Range(0f, 360f), new Vector3(s, 2.6f, s));
                    Place("statue_block", parent, new Vector3(position.x, 0f, position.z), Range(0f, 360f), new Vector3(size.x / 0.4f * 1.25f, 0.9f, size.x / 0.4f * 1.25f));
                    continue;
                }
                bool alongX = size.x >= size.z;
                float length = alongX ? size.x : size.z, depth = alongX ? size.z : size.x;
                int count = Mathf.Max(2, Mathf.CeilToInt(length / 1.25f));
                for (int i = 0; i < count; i++) {
                    float t = (i + 0.5f) / count - 0.5f;
                    Vector3 p = position + (alongX ? Vector3.right : Vector3.forward) * (t * length);
                    p.y = 0f;
                    float s = depth * Range(1.5f, 1.9f);
                    Place(Pick(WallRocks), parent, p, Range(0f, 360f), new Vector3(s, Range(2.1f, 2.7f), s));
                }
                for (int i = 0; i < count; i++) {   // rubble at the foot
                    Vector3 p = position + new Vector3(Range(-size.x, size.x) * 0.6f, 0f, Range(-size.z, size.z) * 0.6f);
                    p.y = 0f;
                    Place(Pick(Pebbles), parent, p + new Vector3(Range(-0.8f, 0.8f), 0f, Range(-0.8f, 0.8f)), Range(0f, 360f), Vector3.one * Range(1.8f, 2.8f), false);
                }
            }
        }

        private static void Islands() {
            var root = new GameObject(LandmarksRoot).transform;
            foreach (Vector3 center in Landmarks) {
                var island = new GameObject($"Island_{center.x:0}_{center.z:0}");
                island.transform.SetParent(root, false);
                island.transform.position = center;
                var box = island.AddComponent<BoxCollider>();
                box.size = new Vector3(3.2f, 2.5f, 2.6f);
                box.center = new Vector3(0f, 1.25f, 0f);
                var modifier = island.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
                Place(Pick(WallRocks), island.transform, center + new Vector3(-0.6f, 0f, 0.2f), Range(0f, 360f), new Vector3(2.4f, 2.8f, 2.4f));
                Place(Pick(WallRocks), island.transform, center + new Vector3(0.8f, 0f, -0.3f), Range(0f, 360f), new Vector3(2f, 2.2f, 2f));
                Place(Pick(BoundaryRocks), island.transform, center + new Vector3(0.2f, 0f, 0.7f), Range(0f, 360f), new Vector3(1.6f, 2.6f, 1.6f));
                for (int i = 0; i < 5; i++)
                    Place(Pick(i < 3 ? Grass : Pebbles), island.transform, center + new Vector3(Range(-2.4f, 2.4f), 0f, Range(-2f, 2f)), Range(0f, 360f), Vector3.one * Range(1.4f, 2f), false);
            }
        }

        private static void Fires(Transform root) {
            foreach (Vector3 p in Campfires) {
                Place("campfire_stones", root, p, Range(0f, 360f), Vector3.one * 2.4f, false);
                Place("campfire_logs", root, p, Range(0f, 360f), Vector3.one * 2.4f);
                var light = new GameObject("FireLight").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.position = p + Vector3.up * 1.2f;
                light.type = LightType.Point;
                light.color = new Color(1f, 0.58f, 0.25f);
                light.range = 9f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;
            }
        }

        // ---------------------------------------------------------------- decor (no collision)

        private static bool NearObstacle(Vector3 p, float margin) {
            foreach (var (_, position, size) in TestSceneSetup.Obstacles)
                if (Mathf.Abs(p.x - position.x) < size.x / 2f + margin && Mathf.Abs(p.z - position.z) < size.z / 2f + margin) return true;
            foreach (Vector3 c in Landmarks)
                if ((p - c).sqrMagnitude < (2.4f + margin) * (2.4f + margin)) return true;
            foreach (Vector3 c in Campfires)
                if ((p - c).sqrMagnitude < 4f) return true;
            return false;
        }

        private static void Decor(Transform root) {
            var parent = new GameObject("Decor").transform;
            parent.SetParent(root, false);
            void Scatter(string[] models, int count, float minR, float maxR, float minS, float maxS, bool shadows) {
                for (int i = 0, tries = 0; i < count && tries < count * 20; tries++) {
                    Vector3 p = OnRing(Mathf.Sqrt(Range(minR * minR, maxR * maxR)), Range(0f, Mathf.PI * 2f));
                    if (NearObstacle(p, 0.6f)) continue;
                    Place(Pick(models), parent, p, Range(0f, 360f), Vector3.one * Range(minS, maxS), shadows);
                    i++;
                }
            }
            Scatter(Grass, 320, 6f, Radius + 1f, 1.3f, 2f, false);
            Scatter(Flowers, 110, 9f, Radius, 1.4f, 2f, false);
            Scatter(Pebbles, 60, 8f, Radius, 1.2f, 2f, false);
            Scatter(Bushes, 40, Radius - 5f, Radius + 1f, 2f, 3f, true);
            Scatter(new[] { "mushroom_redGroup", "mushroom_tanGroup", "stump_old" }, 18, Radius - 5f, Radius, 2.2f, 3f, true);
        }

        // ---------------------------------------------------------------- light and atmosphere

        private static readonly Color FogColor = new(0.17f, 0.24f, 0.22f);

        private static void Lighting() {
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null) {
                sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
                sun.color = new Color(1f, 0.94f, 0.84f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.72f;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.64f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.48f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.20f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 48f;
            RenderSettings.fogEndDistance = 120f;
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam != null) {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = FogColor;
                cam.farClipPlane = 220f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                EditorUtility.SetDirty(data);
            }
        }

        private static void PostProcessing() {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null) {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            T Get<T>() where T : VolumeComponent {
                if (!profile.TryGet(out T c)) {
                    c = profile.Add<T>(true);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
                return c;
            }
            Tonemapping tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            Bloom bloom = Get<Bloom>();
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);
            ColorAdjustments color = Get<ColorAdjustments>();
            color.postExposure.Override(0.25f);
            color.contrast.Override(14f);
            color.saturation.Override(14f);
            Vignette vignette = Get<Vignette>();
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);

            var volume = Object.FindFirstObjectByType<Volume>();
            if (volume == null) volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
#endif
