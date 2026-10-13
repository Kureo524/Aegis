using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI {
    /// <summary>
    /// LoL-style cooldown veil (design doc §7.9): a dark pie covering the part of the cooldown still
    /// left, shrinking clockwise from the top. Drawn with Painter2D.
    /// </summary>
    public sealed class CooldownSweep : VisualElement {
        private float _fraction;
        private static readonly Color Veil = new(0f, 0f, 0f, 0.62f);

        /// <summary>Share of the cooldown still left (0 = ready, 1 = just cast).</summary>
        public float Fraction {
            get => _fraction;
            set {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(value, _fraction)) return;
                _fraction = value;
                MarkDirtyRepaint();
            }
        }

        public CooldownSweep() {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx) {
            if (_fraction <= 0f) return;
            Rect r = contentRect;
            Vector2 c = r.center;
            float radius = Mathf.Max(r.width, r.height);   // beyond the corners: the square is fully covered
            Painter2D p = ctx.painter2D;
            p.fillColor = Veil;
            p.BeginPath();
            p.MoveTo(c);
            // From 12 o'clock, clockwise, over the part still to wait
            float start = -90f + 360f * (1f - _fraction);
            p.Arc(c, radius, new Angle(start, AngleUnit.Degree), new Angle(270f, AngleUnit.Degree));
            p.ClosePath();
            p.Fill();
        }
    }
}
