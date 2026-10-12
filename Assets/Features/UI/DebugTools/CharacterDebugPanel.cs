using System.Collections.Generic;
using Features.Events;
using Features.Stats;
using Systems.EventBusses;
using UnityEngine;
using VContainer;

namespace Features.UI.DebugTools {
    /// <summary>
    /// Test scene panel (IMGUI): shows the player's level, XP, points and main stats, and grants XP;
    /// horde controls (start / stop the spawn, burst of enemies for stress tests).
    /// Only talks to the bus: reads XpChanged / LevelUp / StatChanged / HordeStatus, publishes
    /// XpGranted / HordeDebug.
    /// </summary>
    public class CharacterDebugPanel : MonoBehaviour {
        [SerializeField] private int xpPerClick = 100;
        [SerializeField] private int hordeBurst = 50;
        [SerializeField] private StatType[] shownStats = {
            StatType.MaxHealth, StatType.MaxMana, StatType.PhysicalAttack, StatType.MagicAttack,
            StatType.AttackSpeed, StatType.AttackRange, StatType.Armor, StatType.MagicResist, StatType.MoveSpeed,
        };

        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private readonly Dictionary<StatType, float> _stats = new();
        private XpChangedEvent _xp;
        private int _skillPoints = 1;
        private int _overchargePoints;
        private HordeStatusEvent _horde;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            // At injection time, i.e. before the player publishes its initial snapshot
            _subscriptions.Add(eventBus.Subscribe<XpChangedEvent>(e => _xp = e));
            _subscriptions.Add(eventBus.Subscribe<StatChangedEvent>(e => _stats[e.Type] = e.Value));
            _subscriptions.Add(eventBus.Subscribe<HordeStatusEvent>(e => _horde = e));
            _subscriptions.Add(eventBus.Subscribe<LevelUpEvent>(e => {
                _skillPoints = e.SkillPoints;
                _overchargePoints = e.OverchargePoints;
            }));
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void OnGUI() {
            GUILayout.BeginArea(new Rect(12, 12, 230, 460), GUI.skin.box);
            GUILayout.Label($"Level {_xp.Level}   XP {_xp.Xp} / {(_xp.XpToNext > 0 ? _xp.XpToNext.ToString() : "max")}");
            GUILayout.Label($"Skill points {_skillPoints}   Overcharge {_overchargePoints}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"+{xpPerClick} XP")) _eventBus.Publish(new XpGrantedEvent(xpPerClick));
            if (GUILayout.Button("Level up") && _xp.XpToNext > 0) _eventBus.Publish(new XpGrantedEvent(_xp.XpToNext - _xp.Xp));
            GUILayout.EndHorizontal();

            GUILayout.Label($"Horde: {_horde.Alive} enemies, spawn {(_horde.Spawning ? "ON" : "OFF")}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_horde.Spawning ? "Stop spawn" : "Start spawn")) _eventBus.Publish(new HordeDebugEvent(toggle: true, burst: 0));
            if (GUILayout.Button($"+{hordeBurst}")) _eventBus.Publish(new HordeDebugEvent(toggle: false, burst: hordeBurst));
            GUILayout.EndHorizontal();

            foreach (StatType type in shownStats) {
                _stats.TryGetValue(type, out float value);
                GUILayout.Label($"{type}: {value:0.##}");
            }
            GUILayout.EndArea();
        }
    }
}
