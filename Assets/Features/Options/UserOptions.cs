using System;
using System.IO;
using Features.Events;
using Systems.EventBusses;
using Systems.Inputs;
using UnityEngine;
using VContainer;

namespace Features.Options {
    /// <summary>
    /// The player's own choices (design doc §14.5: UserOptions), saved in JSON on top of the
    /// defaults of <see cref="ControlsSettings"/>. Root scope service: read on demand by whoever
    /// needs a value, every change is saved at once and published (UserOptionsChangedEvent).
    /// The Editor uses its own file, so play sessions and tests never touch the build's choices.
    /// </summary>
    public sealed class UserOptions {
        [Serializable]
        private class Data {
            public CastMode castMode;
        }

        private readonly IEventBus _eventBus;
        private readonly string _path;
        private readonly Data _data;

        [Inject]
        public UserOptions(ControlsSettings defaults, IEventBus eventBus)
            : this(defaults, eventBus, Path.Combine(Application.persistentDataPath,
                                                    Application.isEditor ? "options.editor.json" : "options.json")) { }

        public UserOptions(ControlsSettings defaults, IEventBus eventBus, string path) {
            _eventBus = eventBus;
            _path = path;
            _data = new Data { castMode = defaults != null ? defaults.CastMode : CastMode.QuickWithIndicator };
            Load();
        }

        /// <summary>How R and F cast: aim + click, hold + release, or at once (§7.4).</summary>
        public CastMode CastMode {
            get => _data.castMode;
            set {
                if (_data.castMode == value) return;
                _data.castMode = value;
                Save();
                _eventBus.Publish(new UserOptionsChangedEvent());
            }
        }

        private void Load() {
            try {
                if (File.Exists(_path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(_path), _data);
            }
            catch (Exception e) {
                Debug.LogWarning($"[UserOptions] Could not read {_path}, defaults kept: {e.Message}");
            }
        }

        private void Save() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonUtility.ToJson(_data, prettyPrint: true));
            }
            catch (Exception e) {
                Debug.LogWarning($"[UserOptions] Could not save {_path}: {e.Message}");
            }
        }
    }
}
