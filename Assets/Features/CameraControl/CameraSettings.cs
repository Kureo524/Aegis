using UnityEngine;

namespace Features.CameraControl {
    /// <summary>Tuning of the LoL-style camera (design doc §14.5). Read-only at runtime.</summary>
    [CreateAssetMenu(menuName = "Game/Settings/Camera Settings", fileName = "CameraSettings")]
    public class CameraSettings : ScriptableObject {
        [Header("Follow (locked)")]
        [SerializeField] private Vector3 offset = new(0, 10, -8);
        [SerializeField] private float followSpeed = 10f;
        [SerializeField] private bool startLocked = true;

        [Header("Edge Scrolling (unlocked)")]
        [SerializeField] private bool edgeScrollingEnabled = true;
        [Tooltip("Width in pixels of the screen border that scrolls the camera.")]
        [SerializeField] private float edgeSize = 20f;
        [SerializeField] private float edgeScrollSpeed = 15f;
        [SerializeField] private float scrollSmoothing = 8f;

        [Header("Zoom")]
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 20f;
        [SerializeField] private float zoomSpeed = 10f;

        public Vector3 Offset => offset;
        public float FollowSpeed => followSpeed;
        public bool StartLocked => startLocked;
        public bool EdgeScrollingEnabled => edgeScrollingEnabled;
        public float EdgeSize => edgeSize;
        public float EdgeScrollSpeed => edgeScrollSpeed;
        public float ScrollSmoothing => scrollSmoothing;
        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;
        public float ZoomSpeed => zoomSpeed;
    }
}
