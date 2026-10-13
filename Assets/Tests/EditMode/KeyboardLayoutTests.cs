using System.Collections.Generic;
using NUnit.Framework;
using Systems.EventBusses;
using Systems.Inputs;
using Systems.Inputs.Events;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode {
    /// <summary>
    /// Spell keys, attack-move and stop are bound by position (design doc §4.7), through the real
    /// InputReader: whatever the layout, physical Q W E R are the abilities (A Z E R on AZERTY),
    /// D F the summoner spells and physical A the attack-move (Q on AZERTY). Keys report press and
    /// release; Ctrl + ability key ranks the ability up.
    /// </summary>
    public class KeyboardLayoutTests : InputTestFixture {
        private Keyboard _keyboard;
        private ControlsSettings _settings;
        private InputReader _reader;
        private readonly List<AbilityKeyEvent> _abilities = new();
        private readonly List<bool> _attackMoves = new();

        private void Boot(KeyboardLayout layout) {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _settings = ScriptableObject.CreateInstance<ControlsSettings>();
            var so = new SerializedObject(_settings);
            so.FindProperty("keyboardLayout").enumValueIndex = (int)layout;
            so.ApplyModifiedPropertiesWithoutUndo();

            var bus = new EventBus();
            bus.Subscribe<AbilityKeyEvent>(e => _abilities.Add(e));
            bus.Subscribe<AttackMoveEvent>(e => _attackMoves.Add(e.Pressed));

            _reader = new InputReader(bus, _settings);
            _reader.Initialize();
            _reader.EnableInput(EActionTypes.Game);
        }

        public override void TearDown() {
            _reader?.Dispose();
            Object.DestroyImmediate(_settings);
            _abilities.Clear();
            _attackMoves.Clear();
            base.TearDown();
        }

        [TestCase(KeyboardLayout.Azerty)]
        [TestCase(KeyboardLayout.Qwerty)]
        public void SpellKeys_AreBoundByPosition(KeyboardLayout layout) {
            Boot(layout);
            Press(_keyboard.qKey);
            Press(_keyboard.wKey);
            Press(_keyboard.eKey);
            Press(_keyboard.rKey);
            Press(_keyboard.dKey);
            Press(_keyboard.fKey);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, _abilities.ConvertAll(e => e.Slot));
            Assert.IsTrue(_abilities.TrueForAll(e => !e.Released && !e.LevelUp));
            CollectionAssert.IsEmpty(_attackMoves);
        }

        [Test]
        public void SpellKey_ReportsItsRelease() {
            Boot(KeyboardLayout.Azerty);
            Press(_keyboard.rKey);
            Release(_keyboard.rKey);

            Assert.AreEqual(2, _abilities.Count);
            Assert.IsFalse(_abilities[0].Released);
            Assert.IsTrue(_abilities[1].Released);
            Assert.AreEqual(3, _abilities[1].Slot);
        }

        [Test]
        public void CtrlPlusSpellKey_RanksUp() {
            Boot(KeyboardLayout.Azerty);
            Press(_keyboard.leftCtrlKey);
            Press(_keyboard.qKey);

            Assert.AreEqual(1, _abilities.Count);
            Assert.IsTrue(_abilities[0].LevelUp);
        }

        [TestCase(KeyboardLayout.Azerty)]
        [TestCase(KeyboardLayout.Qwerty)]
        public void AttackMove_IsPhysicalA_PressAndRelease(KeyboardLayout layout) {
            Boot(layout);
            Press(_keyboard.aKey);
            Release(_keyboard.aKey);

            CollectionAssert.AreEqual(new[] { true, false }, _attackMoves);
            CollectionAssert.IsEmpty(_abilities);
        }

        [Test]
        public void SameKeys_OnBothLayouts_AreUntouched() {
            Assert.AreEqual("e", KeyboardLayouts.PhysicalKey(KeyboardLayout.Azerty, 'e'));
            Assert.AreEqual("r", KeyboardLayouts.PhysicalKey(KeyboardLayout.Azerty, 'r'));
            Assert.AreEqual("semicolon", KeyboardLayouts.PhysicalKey(KeyboardLayout.Azerty, 'm'));
        }
    }
}
