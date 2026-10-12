using System.Collections.Generic;
using Features.StateMachines;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode {
    public class GameFlowStateMachineTests {
        private GameObject _go;
        private GameFlowStateMachine _machine;

        [SetUp]
        public void SetUp() {
            _go = new GameObject("StateMachine");
            _machine = _go.AddComponent<GameFlowStateMachine>();

            // Menu -> Loading -> Game -> Pause -> Game
            SetRules(
                (GameState.Menu, new[] { GameState.Loading }),
                (GameState.Loading, new[] { GameState.Game }),
                (GameState.Game, new[] { GameState.Pause }),
                (GameState.Pause, new[] { GameState.Game }));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private void SetRules(params (GameState from, GameState[] to)[] rules) {
            var so = new SerializedObject(_machine);
            SerializedProperty list = so.FindProperty("rules");
            list.ClearArray();
            for (int i = 0; i < rules.Length; i++) {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty rule = list.GetArrayElementAtIndex(i);
                rule.FindPropertyRelative("from").enumValueIndex = (int)rules[i].from;
                SerializedProperty to = rule.FindPropertyRelative("to");
                to.ClearArray();
                for (int j = 0; j < rules[i].to.Length; j++) {
                    to.InsertArrayElementAtIndex(j);
                    to.GetArrayElementAtIndex(j).enumValueIndex = (int)rules[i].to[j];
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void FirstTransition_IsAlwaysAllowed_AndHasNoPreviousState() {
            GameState? from = GameState.Win;
            _machine.Changed += (f, _) => from = f;

            Assert.IsTrue(_machine.TryChange(GameState.Game));
            Assert.IsNull(from);
            Assert.AreEqual(GameState.Game, _machine.CurrentState);
        }

        [Test]
        public void AllowedTransition_IsApplied() {
            _machine.TryChange(GameState.Menu);

            Assert.IsTrue(_machine.TryChange(GameState.Loading));
            Assert.AreEqual(GameState.Loading, _machine.CurrentState);
        }

        [Test]
        public void RefusedTransition_IsLogged_AndStateIsUnchanged() {
            _machine.TryChange(GameState.Menu);

            LogAssert.Expect(LogType.Warning, "[GameFlowStateMachine] Transition refused: Menu -> Game");
            Assert.IsFalse(_machine.TryChange(GameState.Game));
            Assert.AreEqual(GameState.Menu, _machine.CurrentState);
        }

        [Test]
        public void TransitionRequestedFromHandler_IsAppliedAfterCurrentOne() {
            var seen = new List<GameState>();
            _machine.Changed += (_, to) => {
                seen.Add(to);
                if (to == GameState.Loading) _machine.TryChange(GameState.Game);
            };

            _machine.TryChange(GameState.Menu);
            _machine.TryChange(GameState.Loading);

            CollectionAssert.AreEqual(new[] { GameState.Menu, GameState.Loading, GameState.Game }, seen);
        }
    }
}
