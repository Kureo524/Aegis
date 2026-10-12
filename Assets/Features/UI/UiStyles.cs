using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI {
    /// <summary>Small helpers for HUD elements built in code (UI Toolkit inline styles).</summary>
    public static class UiStyles {
        public static void Border(VisualElement e, float width, Color color, float radius) {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        public static void Fill(VisualElement e) {
            e.style.position = Position.Absolute;
            e.style.left = e.style.right = e.style.top = e.style.bottom = 0;
        }

        public static Label Text(string text, float size, Color color, bool bold = true) {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.unityTextOutlineWidth = 1f;
            label.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.85f);
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            return label;
        }
    }
}
