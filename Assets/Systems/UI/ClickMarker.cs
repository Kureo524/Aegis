using UnityEngine;

namespace Systems.UI {
    public class ClickMarker : MonoBehaviour {
        [SerializeField] private float lifetime = 0.5f;
        [SerializeField] private float scaleSpeed = 5f;
        [SerializeField] private float maxScale = 1.5f;

        private float _timer;
        private Vector3 _initialScale;

        private void Awake() {
            _initialScale = transform.localScale;
        }

        private void Update() {
            _timer += Time.deltaTime;
            float t = _timer / lifetime;
            
            // Scale up then down
            float scale = Mathf.Lerp(1f, maxScale, Mathf.Sin(t * Mathf.PI));
            transform.localScale = _initialScale * scale;
            
            // Fade out
            var renderer = GetComponent<Renderer>();
            if (renderer != null) {
                Color c = renderer.material.color;
                c.a = 1f - t;
                renderer.material.color = c;
            }
            
            if (_timer >= lifetime)
                Destroy(gameObject);
        }
    }
}
