using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI {
    /// <summary>
    /// One floating tooltip per document (design doc §7.9): title, a line of facts (rank, cost,
    /// cooldown…) and a text, shown above the hovered element, kept on screen. Styled by the theme
    /// (.ae-tooltip).
    /// </summary>
    public sealed class UiTooltip {
        private readonly VisualElement _root;
        private readonly Label _title, _meta, _text;

        public VisualElement Root => _root;

        public UiTooltip() {
            _root = new VisualElement { pickingMode = PickingMode.Ignore };
            _root.AddToClassList("ae-tooltip");
            _title = new Label { pickingMode = PickingMode.Ignore };
            _title.AddToClassList("ae-tooltip__title");
            _meta = new Label { pickingMode = PickingMode.Ignore };
            _meta.AddToClassList("ae-tooltip__meta");
            _text = new Label { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("ae-tooltip__text");
            _root.Add(_title);
            _root.Add(_meta);
            _root.Add(_text);
            _root.style.display = DisplayStyle.None;
        }

        /// <summary>Shows the tooltip of <paramref name="target"/> while the pointer is over it.</summary>
        public void Attach(VisualElement target, Func<(string title, string meta, string text)> content) {
            target.RegisterCallback<PointerEnterEvent>(_ => {
                (string title, string meta, string text) = content();
                if (string.IsNullOrEmpty(title)) return;
                Show(title, meta, text, target);
            });
            target.RegisterCallback<PointerLeaveEvent>(_ => Hide());
        }

        public void Show(string title, string meta, string text, VisualElement anchor) {
            _title.text = title;
            _meta.text = meta ?? "";
            _meta.style.display = string.IsNullOrEmpty(meta) ? DisplayStyle.None : DisplayStyle.Flex;
            _text.text = text ?? "";
            _text.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            // Above the anchor once the size is known, clamped to the panel
            _root.schedule.Execute(() => Place(anchor));
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

        private void Place(VisualElement anchor) {
            VisualElement parent = _root.parent;
            if (parent == null) return;
            Rect a = parent.WorldToLocal(anchor.worldBound);
            float width = _root.resolvedStyle.width, height = _root.resolvedStyle.height;
            float x = Mathf.Clamp(a.center.x - width / 2f, 8f, parent.layout.width - width - 8f);
            float y = a.yMin - height - 8f;
            if (y < 8f) y = a.yMax + 8f;
            _root.style.left = x;
            _root.style.top = y;
        }
    }
}
