using UnityEngine;

namespace Features.Spells {
    /// <summary>
    /// Ground outline of the aimed spell (design doc §7.5): rectangle for a skillshot, circle at the
    /// cursor for a ground point, sector for a cone, ring under the target for a unit target.
    /// LineRenderer (world space) on a child of the player prefab.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SpellIndicator : MonoBehaviour {
        [SerializeField] private int segments = 48;
        [SerializeField] private float groundOffset = 0.07f;

        private LineRenderer _line;
        private LineRenderer Line {
            get {
                if (_line != null) return _line;
                _line = GetComponent<LineRenderer>();
                _line.useWorldSpace = true;
                _line.loop = true;
                return _line;
            }
        }

        public void SetColor(Color color) {
            var c = new Color(color.r, color.g, color.b, 0.85f);
            Line.startColor = Line.endColor = c;
        }

        public void ShowCircle(Vector3 center, float radius) {
            LineRenderer line = Line;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++) {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, Ground(center.x + Mathf.Cos(a) * radius, center.z + Mathf.Sin(a) * radius));
            }
            gameObject.SetActive(true);
        }

        public void ShowRectangle(Vector3 origin, Vector3 direction, float length, float halfWidth) {
            Vector3 side = Vector3.Cross(Vector3.up, direction) * halfWidth;
            Vector3 end = origin + direction * length;
            LineRenderer line = Line;
            line.positionCount = 4;
            line.SetPosition(0, Ground(origin - side));
            line.SetPosition(1, Ground(end - side));
            line.SetPosition(2, Ground(end + side));
            line.SetPosition(3, Ground(origin + side));
            gameObject.SetActive(true);
        }

        public void ShowCone(Vector3 origin, Vector3 direction, float range, float angle) {
            int arc = Mathf.Max(4, segments / 3);
            LineRenderer line = Line;
            line.positionCount = arc + 2;
            line.SetPosition(0, Ground(origin));
            for (int i = 0; i <= arc; i++) {
                Vector3 d = Quaternion.Euler(0f, -angle / 2f + angle * i / arc, 0f) * direction;
                line.SetPosition(i + 1, Ground(origin + d * range));
            }
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private Vector3 Ground(Vector3 p) => new(p.x, groundOffset, p.z);
        private Vector3 Ground(float x, float z) => new(x, groundOffset, z);
    }
}
