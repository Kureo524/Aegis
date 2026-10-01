using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using JetBrains.Annotations;
using UnityEngine;

namespace Systems.StateMachine {
    
    [Serializable]
    public abstract class GameStateMachine<TId> : MonoBehaviour { 
        [SerializedDictionary("From" , "To")]
        [SerializeField] private SerializedDictionary<TId, List<TId>> allowedSwitches = new ();
        bool _busy;
        
        public State CurrentState { get; private set; }
        public event Action<State, State> Changed;
        
        public class State {
            public TId Id { get; private set; }
            [CanBeNull] public CanvasGroup CanvasToOpen { get; private set; }
            [CanBeNull] public string SceneSlotToOpen { get; private set; }
        
            public State(TId id) => Id = id;

            public State WithCanvasToOpen(CanvasGroup groupToOpen) {
                CanvasToOpen = groupToOpen;
                return this;
            }

            public State WithSceneToOpen(string sceneSlot) {
                SceneSlotToOpen = sceneSlot;
                return this;
            }
        }
        
        public bool TryChange(State next) => Run(next, checkRules: true);
        
        public bool CanChange(TId next) =>
            CurrentState == null || !allowedSwitches.ContainsKey(CurrentState.Id) || allowedSwitches[CurrentState.Id].Contains(next);

        bool Run(State next, bool checkRules)
        {
            if (_busy) return false;
            if (checkRules && !CanChange(next.Id)) return false;

            _busy = true;
            try
            {
                var from = CurrentState;
                CurrentState = next;
                Changed?.Invoke(from, next);
                return true;
            }
            finally { _busy = false; }
        }
    }
}
