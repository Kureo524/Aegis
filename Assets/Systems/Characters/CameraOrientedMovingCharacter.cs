using NUnit.Framework;
using UnityEngine;

namespace Systems.Characters {
    public class CameraOrientedMovingCharacter : MovingCharacter {
        
        [SerializeField] private Camera characterCamera;

        protected override void Awake() {
            base.Awake();
            
            if(!characterCamera) characterCamera = Camera.main;
            Assert.IsNotNull(characterCamera, $"CharacterCamera is null in {gameObject.name}");
        }

        public override void ApplyMovementDirection(Vector2 direction) {
            Transform cam = characterCamera.transform;

            // On aplatit les axes de la caméra sur le plan du sol
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);

            // Cas dégénéré : caméra quasi pile à la verticale, forward ~ (0,-1,0)
            // → on retombe sur l'axe "up" de la caméra, qui pointe alors vers l'avant visuel
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(cam.up, Vector3.up);

            forward.Normalize();

            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

            CurrentDirection = (right * direction.x + forward * direction.y);
            MoveRoutine ??= StartCoroutine(ApplySmoothMovement());
        }
    }
}
