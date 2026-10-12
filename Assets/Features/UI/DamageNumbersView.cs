using System.Collections.Generic;
using Features.Combat;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Floating damage numbers (design doc §6 step 7): pooled UI Toolkit labels following the world
    /// position of each hit, rising and fading. Crits are bigger, yellow and end with "!"; damage
    /// taken by the player is red. Fed by DamageDealtEvent.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DamageNumbersView : MonoBehaviour {
        [SerializeField] private float lifetime = 0.9f;
        [SerializeField] private float riseHeight = 1.2f;
        [SerializeField] private float fontSize = 18f;
        [SerializeField] private float critFontSize = 28f;
        [SerializeField] private Color physicalColor = new(1f, 0.85f, 0.70f);
        [SerializeField] private Color magicColor = new(0.65f, 0.55f, 1f);
        [SerializeField] private Color trueColor = Color.white;
        [SerializeField] private Color critColor = new(1f, 0.85f, 0.15f);
        [SerializeField] private Color playerHurtColor = new(1f, 0.30f, 0.30f);

        private struct Number {
            public Label Label;
            public Vector3 Origin;
            public float Drift;
            public float Age;
            public bool IsCrit;
        }

        private readonly List<Number> _active = new();
        private readonly Stack<Label> _pool = new();
        private readonly List<DamageDealtEvent> _pending = new();
        private readonly DisposableBag _subscriptions = new();
        private Camera _camera;
        private VisualElement _layer;

        [Inject]
        public void Construct(IEventBus eventBus, Camera mainCamera) {
            _camera = mainCamera;
            _subscriptions.Add(eventBus.Subscribe<DamageDealtEvent>(OnDamage));
        }

        private void Start() {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.style.position = Position.Absolute;
            _layer.style.left = _layer.style.right = _layer.style.top = _layer.style.bottom = 0;
            root.Add(_layer);

            foreach (DamageDealtEvent e in _pending) Spawn(e);
            _pending.Clear();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void OnDamage(DamageDealtEvent e) {
            if (e.Amount < 0.5f) return;
            if (_layer == null) _pending.Add(e);
            else Spawn(e);
        }

        private void Spawn(DamageDealtEvent e) {
            Label label = _pool.Count > 0 ? _pool.Pop() : CreateLabel();
            label.text = e.IsCrit ? $"{Mathf.RoundToInt(e.Amount)}!" : Mathf.RoundToInt(e.Amount).ToString();
            label.style.fontSize = e.IsCrit ? critFontSize : fontSize;
            label.style.color = e.TargetIsPlayer ? playerHurtColor
                : e.IsCrit ? critColor
                : e.Type switch { DamageType.Magic => magicColor, DamageType.True => trueColor, _ => physicalColor };
            label.style.display = DisplayStyle.Flex;
            _layer.Add(label);

            _active.Add(new Number {
                Label = label,
                Origin = e.Position,
                Drift = Random.Range(-0.35f, 0.35f),   // spread consecutive hits apart
                IsCrit = e.IsCrit,
            });
        }

        private void Update() {
            if (_layer?.panel == null) return;

            for (int i = _active.Count - 1; i >= 0; i--) {
                Number n = _active[i];
                n.Age += Time.deltaTime;
                float t = n.Age / lifetime;

                if (t >= 1f) {
                    n.Label.RemoveFromHierarchy();
                    _pool.Push(n.Label);
                    _active.RemoveAt(i);
                    continue;
                }

                Vector3 world = n.Origin + Vector3.up * (riseHeight * t) + _camera.transform.right * (n.Drift * t);
                Vector3 screen = _camera.WorldToScreenPoint(world);
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_layer.panel, new Vector2(screen.x, Screen.height - screen.y));

                n.Label.style.left = panelPos.x;
                n.Label.style.top = panelPos.y;
                n.Label.style.opacity = 1f - t * t;
                float pop = n.IsCrit ? 1f + 0.5f * Mathf.Max(0f, 1f - t * 6f) : 1f;   // crits pop at spawn
                n.Label.style.scale = new Scale(new Vector3(pop, pop, 1f));
                n.Label.style.display = screen.z > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                _active[i] = n;
            }
        }

        private static Label CreateLabel() {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextOutlineWidth = 1.5f;
            label.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.9f);
            return label;
        }
    }
}
