using System;
using System.Collections.Generic;
using Features.Characters;
using Features.Events;
using Features.Passives;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Features.Loot {
    /// <summary>
    /// Loot on the ground (design doc §9.5), only ever dropped by the player's passives
    /// (LootDropRequestedEvent: augments, items). Picked up on contact:
    /// - stat anvil → opened for free at the end of the wave (BonusRewardEarnedEvent);
    /// - summoner spell → replaces F for one use (SummonerSpellPickedEvent), only while F holds none.
    /// Untouched loot vanishes after Lifetime, and at the end of the wave. Drops are plain sprites
    /// facing the camera, reused. Game scope service.
    /// </summary>
    public sealed class LootService : IStartable, ITickable, IDisposable {
        private sealed class Drop {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
            public LootKind Kind;
            public Vector3 Position;
            public float ExpiresAt;
            public bool Active;
        }

        private readonly LootSettings _settings;
        private readonly IEventBus _eventBus;
        private readonly Camera _camera;
        private readonly DisposableBag _subscriptions = new();
        private readonly List<Drop> _drops = new();
        private Transform _player;
        private Character _playerCharacter;
        private bool _holdsSummoner;

        public int ActiveCount {
            get {
                int n = 0;
                foreach (Drop d in _drops) if (d.Active) n++;
                return n;
            }
        }

        public LootService(LootSettings settings, IEventBus eventBus, Camera camera) {
            _settings = settings;
            _eventBus = eventBus;
            _camera = camera;
            _subscriptions.Add(eventBus.Subscribe<PlayerSpawnedEvent>(e => {
                _player = e.Player;
                _playerCharacter = e.Player.GetComponent<Character>();
            }));
        }

        public void Start() {
            _subscriptions.Add(_eventBus.Subscribe<LootDropRequestedEvent>(e => Spawn(e.Kind, e.Position)));
            _subscriptions.Add(_eventBus.Subscribe<SummonerSlotChangedEvent>(e => _holdsSummoner = e.HoldsConsumable));
            _subscriptions.Add(_eventBus.Subscribe<RunStateChangedEvent>(e => { if (e.To == RunState.Reward) ClearAll(); }));
        }

        public void Spawn(LootKind kind, Vector3 position) {
            Drop drop = _drops.Find(d => !d.Active);
            if (drop == null) {
                var go = new GameObject("Loot");
                drop = new Drop { GameObject = go, Renderer = go.AddComponent<SpriteRenderer>() };
                _drops.Add(drop);
            }
            drop.Kind = kind;
            drop.Position = new Vector3(position.x, 0f, position.z);
            drop.ExpiresAt = Time.time + _settings.Lifetime;
            drop.Active = true;
            drop.Renderer.sprite = kind == LootKind.StatAnvil ? _settings.StatAnvilSprite : _settings.SummonerSpellSprite;
            drop.GameObject.name = $"Loot ({kind})";
            drop.GameObject.SetActive(true);
        }

        public void Tick() {
            bool canPick = _player != null && _playerCharacter != null && _playerCharacter.IsAlive;
            for (int i = 0; i < _drops.Count; i++) {
                Drop drop = _drops[i];
                if (!drop.Active) continue;
                if (Time.time >= drop.ExpiresAt) {
                    Hide(drop);
                    continue;
                }
                // Bobbing sprite facing the camera
                drop.GameObject.transform.position = drop.Position + Vector3.up * (0.35f + Mathf.Sin(Time.time * 3f + i) * 0.12f);
                if (_camera != null) drop.GameObject.transform.rotation = _camera.transform.rotation;

                if (!canPick) continue;
                Vector3 delta = _player.position - drop.Position;
                delta.y = 0f;
                if (delta.magnitude <= _settings.PickupRadius && TryPickUp(drop)) Hide(drop);
            }
        }

        private bool TryPickUp(Drop drop) {
            switch (drop.Kind) {
                case LootKind.StatAnvil:
                    _eventBus.Publish(new BonusRewardEarnedEvent(RewardKind.FreeStatAnvil));
                    _eventBus.Publish(new GameplayFeedbackEvent("Enclume à stats ! (fin de vague)"));
                    return true;
                case LootKind.SummonerSpell:
                    if (_holdsSummoner || _settings.SummonerSpells.Length == 0) return false;   // stays on the ground
                    var spell = _settings.SummonerSpells[UnityEngine.Random.Range(0, _settings.SummonerSpells.Length)];
                    _eventBus.Publish(new SummonerSpellPickedEvent(spell));
                    return true;
                default:
                    return false;
            }
        }

        private void ClearAll() {
            foreach (Drop drop in _drops) if (drop.Active) Hide(drop);
        }

        private static void Hide(Drop drop) {
            drop.Active = false;
            drop.GameObject.SetActive(false);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            foreach (Drop drop in _drops) if (drop.GameObject != null) Object.Destroy(drop.GameObject);
            _drops.Clear();
        }
    }
}
