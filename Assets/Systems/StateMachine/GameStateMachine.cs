using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.StateMachine {
    
    public abstract class GameStateMachine<TId> : MonoBehaviour { 
        [Serializable]
        public struct Rule {
            public TId from;
            public List<TId> to;
        }

        [SerializeField] private List<Rule> rules = new();
        Dictionary<TId, HashSet<TId>> _allowed;
        
        bool _busy;
        
        readonly Queue<(State next, bool check)> _pending = new();
        
        public State CurrentState { get; private set; }
        public event Action<State, State> Changed;
        
        public class State {
            public TId Id { get; private set; }

            public State(TId id) => Id = id;
        }
        
        void Awake() {
            _allowed = new();
            foreach (var r in rules)
                _allowed[r.from] = new HashSet<TId>(r.to);
        }
        
        public bool TryChange(State next) => Run(next, checkRules: true);
        
        public bool CanChange(TId next) =>
            CurrentState == null
            || !_allowed.TryGetValue(CurrentState.Id, out var set)
            || set.Contains(next);

        // [NEW] Returns true only if the transition was actually applied (not just queued)
        bool Run(State next, bool checkRules)
        {
            if (next == null) return false;
            if (_busy) { _pending.Enqueue((next, checkRules)); return true; }

            _busy = true;
            try
            {
                bool applied = Apply(next, checkRules);
                while (_pending.Count > 0)
                {
                    var (n, c) = _pending.Dequeue();
                    Apply(n, c);
                }
                return applied;
            }
            finally { _busy = false; _pending.Clear(); }
        }

        // [NEW] Returns true if the transition was applied, false if refused
        bool Apply(State next, bool checkRules)
        {
            if (checkRules && !CanChange(next.Id)) {
                Debug.LogWarning($"[GameStateMachine] Transition refused: {(CurrentState != null ? CurrentState.Id.ToString() : "null")} -> {next.Id}");
                return false;
            }
            var from = CurrentState;
            CurrentState = next;
            Changed?.Invoke(from, next);
            return true;
        }
    }
}
