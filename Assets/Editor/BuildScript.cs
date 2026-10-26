#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EditorTools {
    public static class BuildScript {
        private const string TestScene = "Assets/Scenes/Tests/TestScene.unity";
        private const string LinuxOutput = "Builds/Linux/Aegis.x86_64";
        private const string WindowsOutput = "Builds/Windows/Aegis.exe";

        /// <summary>
        /// Builds a standalone Linux player containing only the test scene, so the character,
        /// camera and cursor behaviour can be checked in a real build (where cursor
        /// confinement actually works, unlike in the Editor).
        ///   Unity -batchmode -quit -executeMethod EditorTools.BuildScript.BuildLinuxTest
        /// </summary>
        [MenuItem("Tools/Build Linux (Test Scene)")]
        public static void BuildLinuxTest() => Build(new[] { TestScene }, BuildTarget.StandaloneLinux64, LinuxOutput);

        /// <summary>
        /// Builds the full game: every scene enabled in the build scene list (Menu, then TestScene).
        ///   Unity -batchmode -quit -executeMethod EditorTools.BuildScript.BuildLinuxGame
        /// </summary>
        [MenuItem("Tools/Build Linux (Game)")]
        public static void BuildLinuxGame() => Build(GameScenes(), BuildTarget.StandaloneLinux64, LinuxOutput);

        /// <summary>
        /// Builds the full game for Windows 64-bit (requires the Windows Build Support module).
        ///   Unity -batchmode -quit -executeMethod EditorTools.BuildScript.BuildWindowsGame
        /// </summary>
        [MenuItem("Tools/Build Windows (Game)")]
        public static void BuildWindowsGame() => Build(GameScenes(), BuildTarget.StandaloneWindows64, WindowsOutput);

        private static string[] GameScenes() =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        private static void Build(string[] scenes, BuildTarget target, string locationPathName) {
            Directory.CreateDirectory(Path.GetDirectoryName(locationPathName));

            var options = new BuildPlayerOptions {
                scenes = scenes,
                locationPathName = locationPathName,
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[BuildScript] result={summary.result} " +
                      $"size={summary.totalSize} bytes " +
                      $"errors={summary.totalErrors} " +
                      $"warnings={summary.totalWarnings} " +
                      $"time={summary.totalTime}");

            foreach (BuildStep step in report.steps) {
                foreach (BuildStepMessage msg in step.messages) {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception) {
                        Debug.LogError($"[BuildScript] {step.name}: {msg.content}");
                    }
                }
            }

            // Exit code for CI / command line only: from the Tools menu this would close the Editor.
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
