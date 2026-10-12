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
    /// Letter bindings follow the configured layout (design doc §4.7), through the real InputReader:
    /// on AZERTY, the key that types A (physical Q) is Element1 and the one that types Q (physical A)
    /// is the attack-move, which reports press and release (aim while held).
    /// </summary>
    public class KeyboardLayoutTests : InputTestFixture {
        private Keyboard _keyboard;
        private ControlsSettings _settings;
        private InputReader _reader;
        private readonly List<int> _elements = new();
        private readonly List<bool> _attackMoves = new();

        private void Boot(KeyboardLayout layout) {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _settings = ScriptableObject.CreateInstance<ControlsSettings>();
            var so = new SerializedObject(_settings);
            so.FindProperty("keyboardLayout").enumValueIndex = (int)layout;
            so.ApplyModifiedPropertiesWithoutUndo();

            var bus = new EventBus();
            bus.Subscribe<ElementKeyEvent>(e => _elements.Add(e.Slot));
            bus.Subscribe<AttackMoveEvent>(e => _attackMoves.Add(e.Pressed));

            _reader = new InputReader(bus, _settings);
            _reader.Initialize();
            _reader.EnableInput(EActionTypes.Game);
        }

        public override void TearDown() {
            _reader?.Dispose();
            Object.DestroyImmediate(_settings);
            _elements.Clear();
            _attackMoves.Clear();
            base.TearDown();
        }

        [Test]
        public void Azerty_KeyThatTypesA_IsElement1() {
            Boot(KeyboardLayout.Azerty);
            Press(_keyboard.qKey);

            CollectionAssert.AreEqual(new[] { 0 }, _elements);
            CollectionAssert.IsEmpty(_attackMoves);
        }

        [Test]
        public void Azerty_KeyThatTypesZ_IsElement2() {
            Boot(KeyboardLayout.Azerty);
            Press(_keyboard.wKey);

            CollectionAssert.AreEqual(new[] { 1 }, _elements);
        }

        [Test]
        public void Azerty_KeyThatTypesQ_IsAttackMove_PressAndRelease() {
            Boot(KeyboardLayout.Azerty);
            Press(_keyboard.aKey);
            Release(_keyboard.aKey);

            CollectionAssert.AreEqual(new[] { true, false }, _attackMoves);
            CollectionAssert.IsEmpty(_elements);
        }

        [Test]
        public void Qwerty_KeepsDefaultPositions() {
            Boot(KeyboardLayout.Qwerty);
            Press(_keyboard.aKey);
            Press(_keyboard.qKey);

            CollectionAssert.AreEqual(new[] { 0 }, _elements);
            CollectionAssert.AreEqual(new[] { true }, _attackMoves);
        }

        [Test]
        public void SameKeys_OnBothLayouts_AreUntouched() {
            Assert.AreEqual("e", KeyboardLayouts.PhysicalKey(KeyboardLayout.Azerty, 'e'));
            Assert.AreEqual("r", KeyboardLayouts.PhysicalKey(KeyboardLayout.Azerty, 'r'));
            Assert.AreEqual("semicolon", KeyboardLayouts.PhysicalKey(KeyboardLayout.Azerty, 'm'));
        }
    }
}
