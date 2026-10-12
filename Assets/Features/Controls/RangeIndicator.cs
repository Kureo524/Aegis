using UnityEngine;

namespace Features.Controls {
    /// <summary>Ground circle showing a radius around the character (attack range while aiming
    /// an attack-move, §4.6). LineRenderer on a child of the player prefab, hidden by default.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RangeIndicator : MonoBehaviour {
        [SerializeField] private int segments = 64;
        [SerializeField] private float groundOffset = 0.05f;

        private LineRenderer _line;

        private void Awake() {
            _line = GetComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = false;
            _line.positionCount = segments;
            gameObject.SetActive(false);
        }

        public void Show(float radius) {
            for (int i = 0; i < segments; i++) {
                float angle = i * Mathf.PI * 2f / segments;
                _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, groundOffset, Mathf.Sin(angle) * radius));
            }
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
