using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Stats {
    /// <summary>
    /// A serializable list of (StatType, value) pairs. Used as a field inside
    /// <see cref="StatBlock"/> so designers fill a compact table in the Inspector.
    /// </summary>
    [Serializable]
    public struct StatEntry {
        public StatType Type;
        public float Value;

        public StatEntry(StatType type, float value) {
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// Base stat values for a character (design doc §5.2 / §14.5).
    /// An immutable-at-runtime ScriptableObject: the live values live in a
    /// <see cref="StatController"/>, never here.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Stat Block", fileName = "StatBlock_")]
    public class StatBlock : ScriptableObject {
        [SerializeField] private List<StatEntry> entries = new();

        public IReadOnlyList<StatEntry> Entries => entries;

        public float Get(StatType type) => TryGet(type, out float value) ? value : 0f;

        public bool TryGet(StatType type, out float value) {
            foreach (StatEntry e in entries) {
                if (e.Type != type) continue;
                value = e.Value;
                return true;
            }
            value = 0f;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate() {
            // Detect duplicate entries, which would make Get() ambiguous.
            var seen = new HashSet<StatType>();
            foreach (StatEntry e in entries) {
                if (!seen.Add(e.Type)) {
                    Debug.LogWarning($"[StatBlock] {name}: duplicate entry for {e.Type}.", this);
                }
            }
        }
#endif
    }
}
