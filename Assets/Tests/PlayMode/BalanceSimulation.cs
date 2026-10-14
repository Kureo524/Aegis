using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Features.Augments;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Events;
using Features.Movement;
using Features.Options;
using Features.Run;
using Features.Spells;
using Features.StateMachines;
using Features.Stats;
using NUnit.Framework;
using Systems.EventBusses;
using Systems.Inputs;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>
    /// Balancing tool (design doc §9, not part of the regular suite: [Explicit]). A bot plays Vesna
    /// through the whole test stage at high speed, through the same inputs as the player (quick
    /// cast): it kites, casts Q / W at the nearest enemy, E and Flash away when low, R into groups,
    /// ranks R &gt; Q &gt; W &gt; E, buys an attack build and takes the first augment. One line per wave
    /// (time to the quota, lowest HP, level, gold, items) goes to the file named by the
    /// AEGIS_BALANCE_LOG environment variable (Temp/balance.log by default).
    /// Run it with -testFilter BalanceSimulation.
    /// </summary>
    [Explicit("Balancing tool: long, run on demand.")]
    public class BalanceSimulation {
        private static readonly string[] Build = {
            "Item_RuneSickle", "Item_DuelistGreaves", "Item_TideBreaker", "Item_SunsteelBlade",
            "Item_GiantsBane", "Item_CrimsonChalice",
        };
        private const float Speed = 4f;
        private static readonly int[] RankOrder = { 3, 0, 1, 2 };
        private CastMode _previousMode;

        private IObjectResolver _root, _game;
        private IEventBus _bus;
        private Character _player;
        private AbilityCaster _abilities;
        private Camera _camera;
        private readonly List<Character> _enemies = new();
        private readonly List<string> _log = new();
        private float _nextScan, _waveStart, _quotaAt, _minHp = 1f;
        private int _wave = -1;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.timeScale = 1f;
            if (_root != null) _root.Resolve<UserOptions>().CastMode = _previousMode;
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        [UnityTest, Timeout(3_000_000)]
        public IEnumerator HeroBot_PlaysTheTestStage() {
            yield return SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);
            yield return null;
            _root = VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
            _game = Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
            _bus = _root.Resolve<IEventBus>();
            _previousMode = _root.Resolve<UserOptions>().CastMode;
            _root.Resolve<UserOptions>().CastMode = CastMode.Quick;
            _player = _game.Resolve<EnemyDirector>().Player;
            _abilities = _player.GetComponent<AbilityCaster>();
            _camera = Object.FindFirstObjectByType<CameraController>().GetComponent<Camera>();
            var run = _game.Resolve<RunDirector>();
            var machine = _root.Resolve<GameFlowStateMachine>();
            _bus.Subscribe<PlayerDiedEvent>(_ => _log.Add($"DIED wave={run.WaveNumber} t={Time.time - _waveStart:0}s alive={_enemies.Count(c => c.IsAlive)} " +
                                                       $"lvl={_player.Level.Level} ranks={_abilities.Rank(0)}{_abilities.Rank(1)}{_abilities.Rank(2)}{_abilities.Rank(3)} {BossHealth()}"));

            float end = Time.realtimeSinceStartup + 2400f;
            while (Time.realtimeSinceStartup < end && machine.CurrentState is GameState.Game) {
                Time.timeScale = run.State == RunState.Reward ? 1f : Speed;
                Track(run);
                if (run.State == RunState.Reward) Shop();
                else if (_player.IsAlive) Play();
                yield return null;
            }
            _log.Add($"END state={machine.CurrentState} wave={run.WaveNumber} lives={run.Lives} level={_player.Level.Level}");
            string path = System.Environment.GetEnvironmentVariable("AEGIS_BALANCE_LOG");
            if (string.IsNullOrEmpty(path)) path = Path.Combine(Application.dataPath, "../Temp/balance.log");
            File.WriteAllLines(path, _log);
            Debug.Log(string.Join("\n", _log));
        }

        // ---- measures ----
        private void Track(RunDirector run) {
            if (run.State == RunState.Combat || run.State == RunState.Overtime) {
                if (run.WaveNumber != _wave) {
                    _wave = run.WaveNumber;
                    _waveStart = Time.time;
                    _quotaAt = -1f;
                    _minHp = 1f;
                }
                if (_quotaAt < 0f && run.QuotaReached) _quotaAt = Time.time - _waveStart;
                if (_player.IsAlive) _minHp = Mathf.Min(_minHp, _player.Health.Ratio);
            }
            if (run.State == RunState.Reward && _wave > 0 && (_log.Count == 0 || !_log[^1].StartsWith($"wave {_wave} "))) {
                var inventory = _game.Resolve<Inventory>();
                _log.Add($"wave {_wave} time={Time.time - _waveStart:0}s quota@{_quotaAt:0}s minHp={_minHp:P0} lvl={_player.Level.Level} " +
                         $"gold={_game.Resolve<Wallet>().Gold} lives={run.Lives} AD={_player.Stats.Value(StatType.PhysicalAttack):0} " +
                         $"items=[{string.Join(",", inventory.Items.Select(i => i.name.Replace("Item_", "")))}]");
            }
        }

        // ---- rewards: build order, then the first augment / anvil choice ----
        private void Shop() {
            var shop = _game.Resolve<ShopService>();
            var augments = _game.Resolve<AugmentController>();
            if (shop.AnvilOpen) {
                shop.Choose(0);
                return;
            }
            if (shop.IsOpen) {
                foreach (string id in Build) {
                    ItemDefinition item = _game.Resolve<ShopCatalog>().Items.FirstOrDefault(i => i.name == id);
                    if (item != null && shop.RefusalReason(item) == null) {
                        shop.TryBuy(item);
                        break;
                    }
                }
                shop.Close();
                return;
            }
            if (augments.Phase == AugmentDraftPhase.SlotsFull) augments.ChooseFullSlotsOption(FullSlotsOption.SlotOrGold, 0);
            else if (augments.Phase == AugmentDraftPhase.Choose) augments.Pick(0);
        }

        // ---- fighting ----
        private void Play() {
            for (int slot = 0; slot < 3; slot++)   // each basic ability learned first, then R > Q > W > E
                if (_abilities.Rank(slot) == 0) _abilities.Ranks.TryRankUp(slot);
            foreach (int slot in RankOrder)
                while (_abilities.Ranks.CanRankUp(slot)) _abilities.Ranks.TryRankUp(slot);
            if (Time.time >= _nextScan) {
                _nextScan = Time.time + 0.15f;
                _enemies.Clear();
                foreach (Character c in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
                    if (c.IsAlive && _player.IsHostileTo(c) && c.Definition is EnemyDefinition && c.Definition.DisplayName != "Training Dummy")
                        _enemies.Add(c);
            }
            Character nearest = Nearest();
            if (nearest == null) return;
            Character boss = _enemies.FirstOrDefault(c => c.IsAlive && c.Definition is BossDefinition);
            Character focus = boss != null && Flat(nearest.transform.position - _player.transform.position).magnitude > 2.5f ? boss : nearest;
            float distance = Flat(nearest.transform.position - _player.transform.position).magnitude;
            Vector3 away = Flat(_player.transform.position - Centroid(6f)).normalized;

            if (_player.Health.Ratio < 0.25f) Cast(AbilitySlots.SummonerF, _player.transform.position + away * 4f);
            if (_player.Health.Ratio < 0.45f || distance < 1.6f) Cast(2, _player.transform.position + away * 4f);
            if (CountWithin(14f) >= 6) Cast(3, Centroid(14f));
            Cast(0, focus.transform.position);
            Cast(1, focus.transform.position);

            var movement = _player.GetComponent<CharacterMovement>();
            var attacker = _player.GetComponent<BasicAttacker>();
            bool danger = _enemies.Any(c => c.IsAlive && c.GetComponent<SpellCaster>().IsCasting
                                             && Flat(c.transform.position - _player.transform.position).magnitude < 7f);
            if (distance < 3.5f || danger) {   // kite, and step out of telegraphed spells
                attacker.Cancel();
                movement.SetDestination(_player.transform.position + away * 3f);
            }
            else if (attacker.Target != focus) attacker.Attack(focus);
        }

        private void Cast(int slot, Vector3 at) {
            if (_abilities.Rank(slot) <= 0 || _abilities.CooldownRemaining(slot) > 0f || !_player.Mana.CanSpend(_abilities.ManaCost(slot))) return;
            _bus.Publish(new PointerPositionEvent(_camera.WorldToScreenPoint(at)));
            _bus.Publish(new AbilityKeyEvent(slot));
        }

        private string BossHealth() {
            foreach (Character c in _enemies)
                if (c.IsAlive && c.Definition is BossDefinition) return $"boss={c.Health.Ratio:P0} of {c.Health.Max:0}";
            return "";
        }

        private Character Nearest() {
            Character best = null;
            float bestD = float.MaxValue;
            foreach (Character c in _enemies) {
                if (!c.IsAlive) continue;
                float d = (c.transform.position - _player.transform.position).sqrMagnitude;
                if (d < bestD) {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        private int CountWithin(float radius) => _enemies.Count(c => c.IsAlive && Flat(c.transform.position - _player.transform.position).magnitude < radius);

        private Vector3 Centroid(float radius) {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Character c in _enemies) {
                if (!c.IsAlive || Flat(c.transform.position - _player.transform.position).magnitude > radius) continue;
                sum += c.transform.position;
                n++;
            }
            return n > 0 ? sum / n : _player.transform.position + Vector3.forward;
        }

        private static Vector3 Flat(Vector3 v) {
            v.y = 0f;
            return v;
        }
    }
}
