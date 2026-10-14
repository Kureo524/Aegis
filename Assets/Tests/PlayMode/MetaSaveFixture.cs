using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode {
    /// <summary>
    /// Every PlayMode test boots the real root scope, whose MetaProgress and UserOptions read the
    /// editor saves (meta.editor.json, options.editor.json): they are set aside for the whole run,
    /// so meta nodes bought or options changed while playing in the editor never change what the
    /// tests see (default options: QuickWithIndicator casts), then put back.
    /// </summary>
    [SetUpFixture]
    public class MetaSaveFixture {
        public static readonly string SavePath = Path.Combine(Application.persistentDataPath, "meta.editor.json");
        public static readonly string OptionsPath = Path.Combine(Application.persistentDataPath, "options.editor.json");
        private string _backup, _options;

        [OneTimeSetUp]
        public void SetAside() {
            _backup = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
            _options = File.Exists(OptionsPath) ? File.ReadAllText(OptionsPath) : null;
            Clear();
            if (File.Exists(OptionsPath)) File.Delete(OptionsPath);
        }

        [OneTimeTearDown]
        public void PutBack() {
            if (_backup != null) File.WriteAllText(SavePath, _backup);
            else Clear();
            if (_options != null) File.WriteAllText(OptionsPath, _options);
            else if (File.Exists(OptionsPath)) File.Delete(OptionsPath);
        }

        public static void Clear() {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
