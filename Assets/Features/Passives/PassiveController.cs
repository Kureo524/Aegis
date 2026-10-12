using System;
using System.Collections.Generic;
using Features.Characters;
using UnityEngine;

namespace Features.Passives {
    /// <summary>
    /// The passives of one character (design doc §8.1): its own, its items', its augments'. Ticked
    /// by the character; cleared by Character.Init. Loot drops of any passive bubble up through
    /// <see cref="LootDropped"/>.
    /// </summary>
    public sealed class PassiveController {
        private readonly Character _owner;
        private readonly List<Passive> _passives = new();

        public IReadOnlyList<Passive> All => _passives;

        public event Action<LootKind, Vector3> LootDropped;
        /// <summary>A passive was added, removed or its stacks changed (UI).</summary>
        public event Action Changed;

        public PassiveController(Character owner) => _owner = owner;

        public Passive Add(PassiveDefinition definition, object source = null) {
            if (definition == null) return null;
            var passive = new Passive(definition, _owner, source);
            passive.LootDropped += OnLootDropped;
            passive.Changed += OnPassiveChanged;
            passive.Attach();
            _passives.Add(passive);
            Changed?.Invoke();
            return passive;
        }

        public void Remove(Passive passive) {
            if (passive == null || !_passives.Remove(passive)) return;
            passive.Detach();
            passive.LootDropped -= OnLootDropped;
            passive.Changed -= OnPassiveChanged;
            Changed?.Invoke();
        }

        public void Clear() {
            for (int i = _passives.Count - 1; i >= 0; i--) Remove(_passives[i]);
        }

        public void Tick() {
            for (int i = 0; i < _passives.Count; i++) _passives[i].Tick();
        }

        public void OnWaveEnded() {
            foreach (Passive passive in _passives) passive.OnWaveEnded();
        }

        private void OnLootDropped(LootKind kind, Vector3 position) => LootDropped?.Invoke(kind, position);
        private void OnPassiveChanged(Passive _) => Changed?.Invoke();
    }
}
