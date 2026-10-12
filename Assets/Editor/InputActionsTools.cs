#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Editor;

namespace EditorTools {
    /// <summary>
    /// Regenerates GameActionMaps.cs from the .inputactions with the Input System's own generator.
    /// The importer is supposed to do it on import ("Generate C# Class"), but does not run in every
    /// context (batchmode, edits made outside the Input Actions editor): use this after editing the
    /// actions, and commit both files together.
    ///   Unity -batchmode -quit -executeMethod EditorTools.InputActionsTools.RegenerateCode
    /// </summary>
    public static class InputActionsTools {
        private const string ActionsPath = "Assets/GameAssets/ActionMaps/GameActionMaps.inputactions";
        private const string WrapperPath = "Assets/GameAssets/ActionMaps/GameActionMaps.cs";

        [MenuItem("Tools/Regenerate Input Actions Code")]
        public static void RegenerateCode() {
            InputActionAsset asset = InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
            var options = new InputActionCodeGenerator.Options {
                sourceAssetPath = ActionsPath,
                className = "GameActionMaps",
                namespaceName = "GameAssets.ActionMaps",
            };

            bool changed = InputActionCodeGenerator.GenerateWrapperCode(WrapperPath, asset, options);
            Object.DestroyImmediate(asset);
            if (changed) AssetDatabase.ImportAsset(WrapperPath);
            Debug.Log($"[InputActionsTools] {WrapperPath} {(changed ? "regenerated" : "already up to date")}.");
        }
    }
}
#endif
