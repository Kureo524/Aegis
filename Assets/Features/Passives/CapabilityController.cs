using System.Collections.Generic;

namespace Features.Passives {
    /// <summary>Rules that are not numbers (design doc §5.6), granted by passives, items or augments.</summary>
    public enum Capability {
        /// <summary>Spells can crit (CritChance / CritDamage), which they cannot by default.</summary>
        SpellCrit,
    }

    /// <summary>One counter per capability and per source: granted while at least one source holds it.</summary>
    public sealed class CapabilityController {
        private readonly Dictionary<Capability, List<object>> _sources = new();

        public bool Has(Capability capability) => _sources.TryGetValue(capability, out List<object> s) && s.Count > 0;

        public void Grant(Capability capability, object source) {
            if (!_sources.TryGetValue(capability, out List<object> list)) _sources[capability] = list = new List<object>();
            list.Add(source);
        }

        public void Revoke(Capability capability, object source) {
            if (_sources.TryGetValue(capability, out List<object> list)) list.Remove(source);
        }

        public void Clear() => _sources.Clear();
    }
}
