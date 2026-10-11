namespace Features.Stats {
    /// <summary>
    /// A single modification applied to a stat (design doc §5.2).
    /// Stats are never written to directly: everything goes through modifiers so the
    /// change is traceable, removable and groupable by <see cref="Source"/>.
    /// </summary>
    public class StatModifier {
        public float Value { get; }
        public ModifierType Type { get; }

        /// <summary>
        /// Who applied this modifier (item, passive, spell, level...). Used to remove
        /// or refresh a whole group at once with <see cref="Stat.RemoveAllFromSource"/>.
        /// </summary>
        public object Source { get; }

        public StatModifier(float value, ModifierType type, object source = null) {
            Value = value;
            Type = type;
            Source = source;
        }
    }
}
