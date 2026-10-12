using UnityEngine;
using VContainer;

namespace Features.CameraControl {
    /// <summary>Keeps a sprite facing the camera (same rotation as the camera). For small
    /// standalone visuals like projectiles; characters use CharacterVisual.</summary>
    public class Billboard : MonoBehaviour {
        private Transform _camera;

        [Inject]
        public void Construct(Camera mainCamera) => _camera = mainCamera.transform;

        private void LateUpdate() {
            if (_camera != null) transform.rotation = _camera.rotation;
        }
    }
}
