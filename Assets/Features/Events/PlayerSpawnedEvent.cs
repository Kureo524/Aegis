using Systems.EventBusses;
using UnityEngine;

namespace Features.Events {
    /// <summary>The player character exists (camera follow, HUD…).</summary>
    public readonly struct PlayerSpawnedEvent : IEvent {
        public readonly Transform Player;
        public PlayerSpawnedEvent(Transform player) => Player = player;
    }
}
