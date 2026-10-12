using System.IO;
using Features.Events;
using Features.Options;
using NUnit.Framework;
using Systems.EventBusses;
using Systems.Inputs;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class UserOptionsTests {
        private string _path;
        private ControlsSettings _defaults;

        [SetUp]
        public void SetUp() {
            _path = Path.Combine(Path.GetTempPath(), $"aegis-options-{System.Guid.NewGuid():N}.json");
            _defaults = ScriptableObject.CreateInstance<ControlsSettings>();
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_path)) File.Delete(_path);
            Object.DestroyImmediate(_defaults);
        }

        [Test]
        public void QuickCast_IsOffByDefault_ThenSavedAndPublished() {
            var bus = new EventBus();
            int published = 0;
            bus.Subscribe<UserOptionsChangedEvent>(_ => published++);

            var options = new UserOptions(_defaults, bus, _path);
            Assert.IsFalse(options.QuickCast, "default from ControlsSettings");
            Assert.IsFalse(File.Exists(_path), "nothing saved until a change");

            options.QuickCast = true;
            options.QuickCast = true;   // no change: no save, no event
            Assert.AreEqual(1, published);
            Assert.IsTrue(File.Exists(_path));

            Assert.IsTrue(new UserOptions(_defaults, new EventBus(), _path).QuickCast, "reloaded from the JSON");
        }

        [Test]
        public void CorruptFile_KeepsTheDefaults() {
            File.WriteAllText(_path, "{ not json");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("UserOptions"));
            Assert.IsFalse(new UserOptions(_defaults, new EventBus(), _path).QuickCast);
        }
    }
}
