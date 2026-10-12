using System;
using Features.Characters;
using Features.Combat;
using Features.Movement;
using UnityEngine;
using VContainer;

namespace Features.Spells {
    /// <summary>
    /// Casts a spell on behalf of its character (design doc §7.7): optional cast time (windup) during
    /// which the character stops (unless CanMoveWhileCasting) and the spell is frozen, then
    /// SpellExecutor applies the effect. One cast at a time; dying cancels it.
    /// On the base Character prefab: the player's element and signature casters and, in phase 6,
    /// enemy AI all go through it.
    /// </summary>
    public class SpellCaster : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private BasicAttacker attacker;
        [SerializeField] private CharacterVisual visual;

        private SpellExecutor _executor;
        private SpellDefinition _spell;
        private CastContext _context;
        private int _levels;
        private float _castEnd;

        public bool IsCasting => _spell != null;
        /// <summary>Move / attack orders are ignored while this is true.</summary>
        public bool BlocksOrders => _spell != null && !_spell.CanMoveWhileCasting;

        public event Action<SpellDefinition> CastCompleted;

        [Inject]
        public void Construct(SpellExecutor executor) => _executor = executor;

        public bool Cast(SpellDefinition spell, CastContext context, int levelsAboveOne) {
            if (spell == null || IsCasting || !owner.IsAlive) return false;

            _spell = spell;
            _context = context;
            _levels = levelsAboveOne;
            _castEnd = Time.time + spell.CastTime;

            if (!spell.CanMoveWhileCasting) {
                attacker.Cancel();
                movement.Stop();
            }
            if (visual != null && spell.Targeting != TargetingType.Self) visual.Face(context.Direction);

            if (spell.CastTime <= 0f) Resolve();
            return true;
        }

        private void Update() {
            if (_spell == null) return;
            if (!owner.IsAlive) {
                _spell = null;   // interrupted
                return;
            }
            if (Time.time >= _castEnd) Resolve();
        }

        private void Resolve() {
            SpellDefinition spell = _spell;
            _spell = null;
            _executor.Execute(owner, spell, _context, _levels);
            owner.Events.RaiseSpellCast(spell);
            CastCompleted?.Invoke(spell);
        }
    }
}
