using Features.Stats;
using Systems.EventBusses;

namespace Features.Events {
    /// <summary>
    /// Published whenever a stat's final value changes (design doc §14.3).
    /// Consumed by the UI and by reactive systems (e.g. MaxMana -> ManaPool).
    /// </summary>
    public readonly struct StatChangedEvent : IEvent {
        public readonly StatType Type;
        public readonly float Value;

        public StatChangedEvent(StatType type, float value) {
            Type = type;
            Value = value;
        }
    }
}
