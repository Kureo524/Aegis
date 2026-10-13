using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode {
    /// <summary>
    /// Every PlayMode test boots the real root scope, whose MetaProgress reads the editor save
    /// (meta.editor.json): it is set aside for the whole run, so meta nodes bought while playing in
    /// the editor never change what the tests see, then put back.
    /// </summary>
    [SetUpFixture]
    public class MetaSaveFixture {
        public static readonly string SavePath = Path.Combine(Application.persistentDataPath, "meta.editor.json");
        private string _backup;

        [OneTimeSetUp]
        public void SetAside() {
            _backup = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
            Clear();
        }

        [OneTimeTearDown]
        public void PutBack() {
            if (_backup != null) File.WriteAllText(SavePath, _backup);
            else Clear();
        }

        public static void Clear() {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
