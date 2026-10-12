#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EditorTools {
    public static class BuildScript {
        private const string TestScene = "Assets/Scenes/Tests/TestScene.unity";
        private const string OutputDir = "Builds/Linux";
        private const string ExecutableName = "Aegis.x86_64";

        /// <summary>
        /// Builds a standalone Linux player containing only the test scene, so the character,
        /// camera and cursor behaviour can be checked in a real build (where cursor
        /// confinement actually works, unlike in the Editor).
        ///   Unity -batchmode -executeMethod EditorTools.BuildScript.BuildLinuxTest
        /// </summary>
        [MenuItem("Tools/Build Linux (Test Scene)")]
        public static void BuildLinuxTest() {
            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions {
                scenes = new[] { TestScene },
                locationPathName = Path.Combine(OutputDir, ExecutableName),
                target = BuildTarget.StandaloneLinux64,
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

            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
