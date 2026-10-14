using Systems.EventBusses;

namespace Features.Events {
    /// <summary>A boss entered the fight (boss bar).</summary>
    public readonly struct BossSpawnedEvent : IEvent {
        public readonly string Name;
        public readonly string Title;
        public BossSpawnedEvent(string name, string title) {
            Name = name;
            Title = title;
        }
    }

    /// <summary>An enemy of the horde spawned as an elite carrying items (design doc §9.6).</summary>
    public readonly struct EliteSpawnedEvent : IEvent {
        public readonly string Name;
        public readonly int Items;
        public EliteSpawnedEvent(string name, int items) {
            Name = name;
            Items = items;
        }
    }

    /// <summary>Boss HP and phase (1-based), for the boss bar.</summary>
    public readonly struct BossHealthEvent : IEvent {
        public readonly float Current;
        public readonly float Max;
        public readonly int Phase;
        public BossHealthEvent(float current, float max, int phase) {
            Current = current;
            Max = max;
            Phase = phase;
        }
    }

    /// <summary>The boss of the wave is dead (RunDirector: the wave may end).</summary>
    public readonly struct BossDefeatedEvent : IEvent { }
}
