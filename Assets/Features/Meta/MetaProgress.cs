using System;
using System.Collections.Generic;
using System.IO;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using VContainer;

namespace Features.Meta {
    /// <summary>
    /// The player's persistent progression (design doc §9.7, MetaSave): currency and node levels,
    /// saved as JSON in persistentDataPath (meta.json; meta.editor.json in the editor, so tests
    /// and the editor never touch the build's save). Earns the currency on RunEndedEvent.
    /// Root scope service.
    /// </summary>
    public sealed class MetaProgress : IDisposable {
        [Serializable]
        private class Data {
            public int currency;
            public List<string> nodes = new();
            public List<int> levels = new();
        }

        private readonly MetaSettings _settings;
        private readonly IEventBus _eventBus;
        private readonly string _path;
        private readonly IDisposable _subscription;
        private Data _data = new();

        public int Currency => _data.currency;
        public MetaSettings Settings => _settings;
        /// <summary>Currency earned by the last finished run (end screen).</summary>
        public int LastReward { get; private set; }

        [Inject]
        public MetaProgress(MetaSettings settings, IEventBus eventBus)
            : this(settings, eventBus, Path.Combine(Application.persistentDataPath, Application.isEditor ? "meta.editor.json" : "meta.json")) { }

        public MetaProgress(MetaSettings settings, IEventBus eventBus, string path) {
            _settings = settings;
            _eventBus = eventBus;
            _path = path;
            Load();
            _subscription = eventBus.Subscribe<RunEndedEvent>(e => Earn(_settings != null ? _settings.Reward(e.Won, e.WavesCleared) : 0));
        }

        public int Level(MetaNodeDefinition node) {
            if (node == null) return 0;
            int i = _data.nodes.IndexOf(node.Id);
            return i >= 0 ? _data.levels[i] : 0;
        }

        /// <summary>Why <paramref name="node"/> cannot be bought now (null: it can).</summary>
        public string RefusalReason(MetaNodeDefinition node) {
            if (node == null) return "?";
            int level = Level(node);
            if (level >= node.MaxLevel) return "Niveau maximum";
            foreach (MetaNodeDefinition p in node.Prerequisites)
                if (Level(p) <= 0) return $"Nécessite « {p.DisplayName} »";
            return Currency < node.Cost(level + 1) ? $"Pas assez d'{_settings?.CurrencyName ?? "éclats"}" : null;
        }

        public bool TryBuy(MetaNodeDefinition node) {
            if (RefusalReason(node) != null) return false;
            int level = Level(node);
            _data.currency -= node.Cost(level + 1);
            int i = _data.nodes.IndexOf(node.Id);
            if (i >= 0) _data.levels[i] = level + 1;
            else {
                _data.nodes.Add(node.Id);
                _data.levels.Add(1);
            }
            Save();
            _eventBus.Publish(new MetaChangedEvent());
            return true;
        }

        public void Earn(int amount) {
            LastReward = Mathf.Max(0, amount);
            _eventBus.Publish(new MetaRewardEvent(LastReward, _settings != null ? _settings.CurrencyName : "éclats"));
            if (LastReward == 0) return;
            _data.currency += LastReward;
            Save();
            _eventBus.Publish(new MetaChangedEvent());
        }

        /// <summary>Forgets everything (tests, a "reset progression" option later).</summary>
        public void ResetAll() {
            _data = new Data();
            Save();
            _eventBus.Publish(new MetaChangedEvent());
        }

        private void Load() {
            try {
                if (File.Exists(_path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(_path), _data);
            }
            catch (Exception e) {
                Debug.LogWarning($"[MetaProgress] Unreadable save {_path}: {e.Message}");
                _data = new Data();
            }
            while (_data.levels.Count < _data.nodes.Count) _data.levels.Add(0);
        }

        private void Save() {
            try {
                File.WriteAllText(_path, JsonUtility.ToJson(_data, prettyPrint: true));
            }
            catch (Exception e) {
                Debug.LogWarning($"[MetaProgress] Cannot save {_path}: {e.Message}");
            }
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
