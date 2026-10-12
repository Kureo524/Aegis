using UnityEngine;

namespace Features.Controls {
    /// <summary>Ground circle showing a radius around the character (attack range while aiming
    /// an attack-move, §4.6). LineRenderer on a child of the player prefab, inactive by default.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RangeIndicator : MonoBehaviour {
        [SerializeField] private int segments = 64;
        [SerializeField] private float groundOffset = 0.05f;

        private LineRenderer _line;

        public bool IsShown => gameObject.activeSelf;
        public float Radius { get; private set; }

        // Fetched lazily: the object starts inactive, so Awake has not run when Show is first called.
        private LineRenderer Line {
            get {
                if (_line != null) return _line;
                _line = GetComponent<LineRenderer>();
                _line.loop = true;
                _line.useWorldSpace = false;
                return _line;
            }
        }

        public void Show(float radius) {
            Radius = radius;
            LineRenderer line = Line;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++) {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, groundOffset, Mathf.Sin(angle) * radius));
            }
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
