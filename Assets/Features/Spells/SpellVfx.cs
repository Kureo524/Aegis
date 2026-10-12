using System;
using UnityEngine;

namespace Features.Spells {
    /// <summary>
    /// Short flash of a spell's shape on the ground (circle, cone) in the spell color: grows a bit and
    /// fades out. Placeholder VFX, pooled through PrefabPool.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SpellVfx : MonoBehaviour {
        [SerializeField] private int segments = 48;
        [SerializeField] private float groundOffset = 0.06f;
        [SerializeField] private float width = 0.12f;

        private LineRenderer _line;
        private Action<Component> _release;
        private Color _color;
        private float _duration, _age;

        private LineRenderer Line => _line != null ? _line : _line = GetComponent<LineRenderer>();

        public void PlayRing(Vector3 center, float radius, Color color, float duration, Action<Component> release) {
            Begin(color, duration, release);
            LineRenderer line = Line;
            line.loop = true;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++) {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, groundOffset, center.z + Mathf.Sin(a) * radius));
            }
        }

        /// <summary>Rectangle outline from <paramref name="origin"/> along <paramref name="direction"/> (charges, skillshots).</summary>
        public void PlayLine(Vector3 origin, Vector3 direction, float length, float halfWidth, Color color, float duration,
                             Action<Component> release) {
            Begin(color, duration, release);
            LineRenderer line = Line;
            line.loop = true;
            direction.y = 0f;
            direction.Normalize();
            Vector3 side = new Vector3(direction.z, 0f, -direction.x) * halfWidth;
            Vector3 end = origin + direction * length;
            line.positionCount = 4;
            line.SetPosition(0, Flat(origin - side));
            line.SetPosition(1, Flat(end - side));
            line.SetPosition(2, Flat(end + side));
            line.SetPosition(3, Flat(origin + side));
        }

        private Vector3 Flat(Vector3 p) => new(p.x, groundOffset, p.z);

        public void PlayCone(Vector3 origin, Vector3 direction, float range, float angle, Color color, float duration,
                             Action<Component> release) {
            Begin(color, duration, release);
            LineRenderer line = Line;
            line.loop = true;
            int arc = Mathf.Max(4, segments / 3);
            line.positionCount = arc + 2;
            line.SetPosition(0, new Vector3(origin.x, groundOffset, origin.z));
            for (int i = 0; i <= arc; i++) {
                float a = -angle / 2f + angle * i / arc;
                Vector3 d = Quaternion.Euler(0f, a, 0f) * direction;
                line.SetPosition(i + 1, new Vector3(origin.x + d.x * range, groundOffset, origin.z + d.z * range));
            }
        }

        private void Begin(Color color, float duration, Action<Component> release) {
            _color = color;
            _duration = Mathf.Max(0.05f, duration);
            _age = 0f;
            _release = release;
            Line.useWorldSpace = true;
            Line.widthMultiplier = width;
            Apply(1f);
        }

        private void Update() {
            _age += Time.deltaTime;
            float t = _age / _duration;
            if (t >= 1f) {
                Action<Component> release = _release;
                _release = null;
                release?.Invoke(this);
                return;
            }
            Apply(1f - t);
            Line.widthMultiplier = width * (1f + t);
        }

        private void Apply(float alpha) {
            var c = new Color(_color.r, _color.g, _color.b, alpha);
            Line.startColor = Line.endColor = c;
        }
    }
}
