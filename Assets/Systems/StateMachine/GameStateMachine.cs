using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.StateMachine {

    /// <summary>
    /// Generic state machine whose allowed transitions are data (serialized rules, edited in the
    /// Inspector). Unity cannot serialize an open generic, so each enum gets a concrete subclass
    /// (e.g. GameFlowStateMachine : GameStateMachine&lt;GameState&gt;).
    ///
    /// A state with no rule entry can go anywhere; the very first transition is always allowed.
    /// </summary>
    public abstract class GameStateMachine<TId> : MonoBehaviour where TId : struct, Enum {
        [Serializable]
        public struct Rule {
            public TId from;
            public List<TId> to;
        }

        [SerializeField] private List<Rule> rules = new();

        private Dictionary<TId, HashSet<TId>> _allowed;
        private bool _busy;
        private readonly Queue<TId> _pending = new();

        public bool HasState { get; private set; }
        public TId CurrentState { get; private set; }

        /// <summary>(from, to). <c>from</c> is null for the very first state.</summary>
        public event Action<TId?, TId> Changed;

        /// <summary>
        /// Requests a transition. Returns false if the rules refuse it (the refusal is logged).
        /// A request made from inside a <see cref="Changed"/> handler is queued and applied right
        /// after the current one; it then returns true, and a later refusal is still logged.
        /// </summary>
        public bool TryChange(TId next) {
            if (_busy) {
                _pending.Enqueue(next);
                return true;
            }

            _busy = true;
            try {
                bool applied = Apply(next);
                while (_pending.Count > 0) Apply(_pending.Dequeue());
                return applied;
            }
            finally {
                _busy = false;
                _pending.Clear();
            }
        }

        public bool CanChange(TId next) {
            if (!HasState) return true;
            return !Allowed.TryGetValue(CurrentState, out HashSet<TId> set) || set.Contains(next);
        }

        // Built lazily so the machine works without Awake (EditMode tests, inactive prefabs).
        private Dictionary<TId, HashSet<TId>> Allowed {
            get {
                if (_allowed != null) return _allowed;
                _allowed = new Dictionary<TId, HashSet<TId>>();
                foreach (Rule r in rules) _allowed[r.from] = new HashSet<TId>(r.to);
                return _allowed;
            }
        }

        private bool Apply(TId next) {
            if (!CanChange(next)) {
                Debug.LogWarning($"[{GetType().Name}] Transition refused: {CurrentState} -> {next}", this);
                return false;
            }

            TId? from = HasState ? CurrentState : null;
            CurrentState = next;
            HasState = true;
            Changed?.Invoke(from, next);
            return true;
        }
    }
}
