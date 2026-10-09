using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace Systems.Characters {
    [RequireComponent(typeof(Rigidbody))]
    public class MovingCharacter : MonoBehaviour {
        protected Rigidbody Rb;
        protected Vector3 CurrentDirection;
        private Vector3 _lastDirection;   // gardée pour décélérer dans le bon sens
        private float _rampProgress;      // 0 = arrêté, 1 = vitesse max
        protected Coroutine MoveRoutine;

        [SerializeField] private float movementSpeed = 5f;
        [SerializeField] private float rampTime = 0.25f; // durée pour passer de 0 à la vitesse max (et inversement)
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public Vector3 CurrentVelocity => Rb.linearVelocity;

        protected virtual void Awake() {
            Rb = GetComponent<Rigidbody>();
            Assert.IsNotNull(Rb, $"Rigidbody component is null in {gameObject.name}");
        }

        public virtual void ApplyMovementDirection(Vector2 direction) {
            CurrentDirection = new Vector3(direction.x, 0, direction.y).normalized;
            MoveRoutine ??= StartCoroutine(ApplySmoothMovement());
        }

        protected IEnumerator ApplySmoothMovement() {
            if (!Rb) {
                MoveRoutine = null;
                yield break;
            }
            var waitForFixed = new WaitForFixedUpdate();
            float step = Time.fixedDeltaTime / Mathf.Max(rampTime, 0.0001f);

            while (true) {
                yield return waitForFixed;

                bool hasInput = CurrentDirection.sqrMagnitude > 0.01f;
                if (hasInput) _lastDirection = CurrentDirection;

                _rampProgress = Mathf.MoveTowards(_rampProgress, hasInput ? 1f : 0f, step);

                float speed = movementSpeed * movementCurve.Evaluate(_rampProgress);
                Vector3 velocity = _lastDirection * speed;
                velocity.y = Rb.linearVelocity.y; // on ne touche pas à la gravité / aux sauts

                Rb.linearVelocity = velocity;

                // Plus d'input et complètement arrêté : on coupe la coroutine
                if (!hasInput && _rampProgress <= 0f) break;
            }

            Rb.linearVelocity = new Vector3(0f, Rb.linearVelocity.y, 0f);
            MoveRoutine = null;
        }
    }
}