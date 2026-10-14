#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Features.Characters;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Animated characters (design doc §3), called by <see cref="ProjectSetup"/>: each sheet of
    /// Assets/GraphicAssets/Sprites/Characters/LoL (frames pre-rendered in Blender from the League of
    /// Legends models, see the README there) is sliced from its .sheet.json (rect and feet pivot of
    /// every frame), turned into a SpriteAnimationSet and given to its character definitions, with
    /// the LoL names of the creatures. Re-runnable: sheets and sets are refreshed, nothing else.
    /// </summary>
    public static class CharacterSpritesSetup {
        public const string SheetsDir = "Assets/GraphicAssets/Sprites/Characters/LoL";
        private const string AnimDir = "Assets/Data/Characters/Animations";
        private const string Enemies = "Assets/Data/Characters/Enemies";

        // sheet → (definitions using it, display name, null = keep)
        private static readonly (string sheet, string[] definitions, string displayName)[] Looks = {
            ("Ezreal", new[] { ProjectSetup.TestHeroPath }, null),
            ("MeleeMinion", new[] { $"{Enemies}/Enemy_Goblin.asset" }, "Sbire de mêlée"),
            ("CasterMinion", new[] { $"{Enemies}/Enemy_SkeletonArcher.asset" }, "Sbire lanceur"),
            ("SuperMinion", new[] { $"{Enemies}/Enemy_Brute.asset" }, "Super-sbire"),
            ("MurkWolf", new[] { $"{Enemies}/Enemy_Wolf.asset" }, "Loup sombre"),
            ("Gromp", new[] { $"{Enemies}/Enemy_SkeletonMage.asset" }, "Gromp"),
            ("Voidgrub", new[] { $"{Enemies}/Enemy_GoblinShaman.asset" }, "Larve du Néant"),
            ("Krug", new[] { $"{Enemies}/Enemy_StoneGolem.asset" }, "Krug"),
            ("RiftHerald", new[] { "Assets/Data/Characters/Bosses/Boss_GoblinKing.asset" }, "Héraut de la Faille"),
            ("TargetDummy", new[] { "Assets/Data/Characters/TrainingDummy/Enemy_TrainingDummy.asset" }, "Mannequin d'entraînement"),
        };

        // Ezreal's own clip per spell
        private static readonly (string clip, string spell)[] EzrealSpells = {
            ("cast_Q", "Assets/Data/Spells/Ezreal/Spell_Ezreal_Q.asset"),
            ("cast_W", "Assets/Data/Spells/Ezreal/Spell_Ezreal_W.asset"),
            ("cast_E", "Assets/Data/Spells/Ezreal/Spell_Ezreal_E.asset"),
            ("cast_R", "Assets/Data/Spells/Ezreal/Spell_Ezreal_R.asset"),
        };

        [System.Serializable] private class Frame { public string clip; public float[] rect; public float[] pivot; }
        [System.Serializable] private class Clip { public string name; public int first; public int count; public float duration; public bool loop; }
        [System.Serializable] private class SheetData { public float ppu; public Frame[] frames; public Clip[] clips; }
        private class Sheet { public float ppu; public List<Frame> frames; public Dictionary<string, Clip> clips; }

        [MenuItem("Tools/Import Character Sprites")]
        public static void Run() {
            if (!Directory.Exists(SheetsDir)) return;
            Directory.CreateDirectory(AnimDir);
            foreach (var (sheetName, definitions, displayName) in Looks) {
                string png = $"{SheetsDir}/{sheetName}.png";
                string json = $"{SheetsDir}/{sheetName}.sheet.json";
                if (!File.Exists(png) || !File.Exists(json)) {
                    Debug.LogWarning($"[CharacterSpritesSetup] Missing sheet {png}");
                    continue;
                }
                Sheet sheet = Parse(File.ReadAllText(json));
                Sprite[] sprites = Slice(png, sheetName, sheet);
                SpriteAnimationSet set = BuildSet(sheetName, sheet, sprites, definitions);
                foreach (string path in definitions) Assign(path, set, displayName);
            }
            AddShadow();
            AssetDatabase.SaveAssets();
        }

        private const string ShadowPath = "Assets/GraphicAssets/Sprites/VFX/BlobShadow.png";

        // A soft shadow under every character: billboards cast none, it grounds them
        private static void AddShadow() {
            var importer = (TextureImporter)AssetImporter.GetAtPath(ShadowPath);
            if (importer != null && importer.textureType != TextureImporterType.Sprite) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 128;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowPath);
            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPrefabSetup.BasePrefabPath);
            var character = root.GetComponent<Character>();
            var so = new SerializedObject(character);
            if (so.FindProperty("shadow").objectReferenceValue == null) {
                var go = new GameObject("Shadow");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.sortingOrder = -10;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                so.FindProperty("shadow").objectReferenceValue = go.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefabSetup.BasePrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static Sheet Parse(string text) {
            var data = JsonUtility.FromJson<SheetData>(text);
            return new Sheet { ppu = data.ppu, frames = data.frames.ToList(), clips = data.clips.ToDictionary(c => c.name) };
        }

        private static Sprite[] Slice(string png, string sheetName, Sheet sheet) {
            var importer = (TextureImporter)AssetImporter.GetAtPath(png);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = sheet.ppu;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = new List<SpriteRect>();
            for (int i = 0; i < sheet.frames.Count; i++) {
                Frame f = sheet.frames[i];
                rects.Add(new SpriteRect {
                    name = $"{sheetName}_{i:000}",
                    rect = new Rect(f.rect[0], f.rect[1], f.rect[2], f.rect[3]),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(f.pivot[0], f.pivot[1]),
                    spriteID = StableId($"{sheetName}_{i:000}"),   // same id on every re-run: references survive
                });
            }
            provider.SetSpriteRects(rects.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        }

        private static GUID StableId(string name) {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("aegis-sprite-" + name));
            return new GUID(string.Concat(hash.Select(x => x.ToString("x2"))));
        }

        private static SpriteAnimationSet BuildSet(string sheetName, Sheet sheet, Sprite[] sprites, string[] definitions) {
            string path = $"{AnimDir}/Anim_{sheetName}.asset";
            var set = AssetDatabase.LoadAssetAtPath<SpriteAnimationSet>(path);
            if (set == null) {
                set = ScriptableObject.CreateInstance<SpriteAnimationSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            var so = new SerializedObject(set);
            void Fill(string property, string clipName) {
                SerializedProperty p = so.FindProperty(property);
                if (!sheet.clips.TryGetValue(clipName, out Clip c)) {
                    p.FindPropertyRelative("Frames").arraySize = 0;
                    return;
                }
                FillClip(p, c, sprites);
            }
            Fill("idle", "idle");
            Fill("run", "run");
            Fill("attack", "attack");
            Fill("hit", "hit");
            Fill("death", "death");
            Fill("dash", "dash");
            Fill("cast", "cast");

            var spells = sheetName == "Ezreal" ? EzrealSpells : System.Array.Empty<(string, string)>();
            SerializedProperty list = so.FindProperty("spellClips");
            list.arraySize = 0;
            foreach (var (clip, spellPath) in spells) {
                if (!sheet.clips.TryGetValue(clip, out Clip c)) continue;
                list.arraySize++;
                SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("Spell").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SpellDefinition>(spellPath);
                FillClip(e.FindPropertyRelative("Clip"), c, sprites);
            }
            // The run clip is paced on the move speed of the character it was made for
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(definitions[0]);
            if (definition != null && definition.BaseStats != null && definition.BaseStats.TryGet(StatType.MoveSpeed, out float speed) && speed > 0f)
                so.FindProperty("runReferenceSpeed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            return set;
        }

        private static void FillClip(SerializedProperty p, Clip c, Sprite[] sprites) {
            SerializedProperty frames = p.FindPropertyRelative("Frames");
            frames.arraySize = c.count;
            for (int i = 0; i < c.count; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[c.first + i];
            p.FindPropertyRelative("Duration").floatValue = Mathf.Max(0.05f, c.duration);
            p.FindPropertyRelative("Loop").boolValue = c.loop;
        }

        private static void Assign(string path, SpriteAnimationSet set, string displayName) {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (definition == null) {
                Debug.LogWarning($"[CharacterSpritesSetup] No definition at {path}");
                return;
            }
            var so = new SerializedObject(definition);
            so.FindProperty("animations").objectReferenceValue = set;
            so.FindProperty("sprite").objectReferenceValue = set.Still;
            string portraitPath = $"{SheetsDir}/Portrait_{set.name.Replace("Anim_", "")}.png";
            if (AssetImporter.GetAtPath(portraitPath) is TextureImporter pi && pi.textureType != TextureImporterType.Sprite) {
                pi.textureType = TextureImporterType.Sprite;
                pi.alphaIsTransparency = true;
                pi.SaveAndReimport();
            }
            var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
            if (portrait != null) so.FindProperty("portrait").objectReferenceValue = portrait;
            if (displayName != null) so.FindProperty("displayName").stringValue = displayName;
            if (definition is Features.Enemies.BossDefinition) so.FindProperty("title").stringValue = "Gardien du Néant";
            // Body as tall as the sprite stands: health bar over the head, clicks on the body
            if (set.Still != null) {
                SerializedProperty body = so.FindProperty("bodySize");
                body.vector2Value = new Vector2(body.vector2Value.x, Mathf.Round(set.Still.bounds.size.y * 0.92f * 100f) / 100f);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
    }
}
#endif
