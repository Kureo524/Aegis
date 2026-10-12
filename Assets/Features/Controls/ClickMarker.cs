using UnityEngine;

namespace Features.Controls {
    /// <summary>Ground feedback for a pointer command: pulses, fades out, then destroys itself.</summary>
    [RequireComponent(typeof(Renderer))]
    public class ClickMarker : MonoBehaviour {
        [SerializeField] private float lifetime = 0.5f;
        [SerializeField] private float maxScale = 1.5f;

        private Renderer _renderer;
        private Material _material;
        private Color _color = Color.white;
        private float _timer;
        private Vector3 _initialScale;

        private void Awake() {
            _renderer = GetComponent<Renderer>();
            _material = _renderer.material;   // per-instance copy, destroyed with the marker
            _color = _material.color;
            _initialScale = transform.localScale;
        }

        public void SetColor(Color color) {
            _color = color;
            _material.color = color;
        }

        private void Update() {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / lifetime);

            transform.localScale = _initialScale * Mathf.Lerp(1f, maxScale, Mathf.Sin(t * Mathf.PI));

            Color c = _color;
            c.a = 1f - t;
            _material.color = c;

            if (_timer >= lifetime) Destroy(gameObject);
        }

        private void OnDestroy() {
            if (_material != null) Destroy(_material);
        }
    }
}
