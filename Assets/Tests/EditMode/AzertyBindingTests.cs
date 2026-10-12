using GameAssets.ActionMaps;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode {
    /// <summary>
    /// Gameplay letter keys are bound by displayed character (<c>&lt;Keyboard&gt;/#(a)</c>), not
    /// by physical position, so they follow the player's layout (design doc §4.7).
    /// Here the keyboard is relabelled as AZERTY.
    /// </summary>
    public class AzertyBindingTests : InputTestFixture {
        private Keyboard _keyboard;
        private GameActionMaps _maps;

        public override void Setup() {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            // AZERTY: physical Q shows "A", physical A shows "Q", physical W shows "Z", physical Z shows "W"
            SetKeyInfo(Key.Q, "A");
            SetKeyInfo(Key.A, "Q");
            SetKeyInfo(Key.W, "Z");
            SetKeyInfo(Key.Z, "W");

            _maps = new GameActionMaps();
            _maps.Gameplay.Enable();
        }

        public override void TearDown() {
            // The generated Dispose() calls Object.Destroy, which is not allowed in EditMode
            UnityEngine.Object.DestroyImmediate(_maps.asset);
            base.TearDown();
        }

        [Test]
        public void KeyLabelledA_TriggersElement1() {
            Press(_keyboard.qKey);
            Assert.IsTrue(_maps.Gameplay.Element1.WasPerformedThisFrame());
            Assert.IsFalse(_maps.Gameplay.AttackMove.WasPerformedThisFrame());
        }

        [Test]
        public void KeyLabelledZ_TriggersElement2() {
            Press(_keyboard.wKey);
            Assert.IsTrue(_maps.Gameplay.Element2.WasPerformedThisFrame());
        }

        [Test]
        public void KeyLabelledQ_TriggersAttackMove() {
            Press(_keyboard.aKey);
            Assert.IsTrue(_maps.Gameplay.AttackMove.WasPerformedThisFrame());
            Assert.IsFalse(_maps.Gameplay.Element1.WasPerformedThisFrame());
        }
    }
}
