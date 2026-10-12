using Features.Combat;
using Systems.EventBusses;
using UnityEngine;

namespace Features.Events {
    /// <summary>A hit landed (damage numbers, audio, VFX). Published by the damaged character.</summary>
    public readonly struct DamageDealtEvent : IEvent {
        public readonly Vector3 Position;      // above the target's head
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly bool IsCrit;
        public readonly bool TargetIsPlayer;

        public DamageDealtEvent(Vector3 position, float amount, DamageType type, bool isCrit, bool targetIsPlayer) {
            Position = position;
            Amount = amount;
            Type = type;
            IsCrit = isCrit;
            TargetIsPlayer = targetIsPlayer;
        }
    }

    /// <summary>An enemy died (design doc §14.3): XP and gold for the player, loot, HUD.</summary>
    public readonly struct EnemyKilledEvent : IEvent {
        public readonly int Xp;
        public readonly int Gold;
        public readonly Vector3 Position;

        public EnemyKilledEvent(int xp, int gold, Vector3 position) {
            Xp = xp;
            Gold = gold;
            Position = position;
        }
    }

    /// <summary>Player HP changed (HUD).</summary>
    public readonly struct PlayerHealthChangedEvent : IEvent {
        public readonly float Current;
        public readonly float Max;

        public PlayerHealthChangedEvent(float current, float max) {
            Current = current;
            Max = max;
        }
    }

    /// <summary>The player died. Nothing reacts yet (GameOver comes with the run loop, phase 7).</summary>
    public readonly struct PlayerDiedEvent : IEvent { }
}
