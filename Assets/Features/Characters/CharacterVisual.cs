using Features.Combat;
using Features.Movement;
using UnityEngine;
using VContainer;

namespace Features.Characters {
    /// <summary>
    /// 2.5D look of a character (design doc §3): a sprite on a child of the physics root that always
    /// faces the camera (billboard: same rotation as the camera, so it reads flat on screen with no
    /// perspective skew), flipped left / right with the horizontal movement on screen.
    /// Sprites are drawn facing right.
    ///
    /// Basic attack feedback (no animator for hundreds of sprites): during the windup the sprite
    /// leans back and squashes, at the impact it lunges towards the target, so the windup reads as
    /// an attack and not as a pause.
    ///
    /// Statuses (§5.7) tint the sprite (stun yellow and shaking, root green, silence purple, slow
    /// blue, burn orange, bleed red) so a CC reads at a glance, on hundreds of enemies too.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterVisual : MonoBehaviour {
        [SerializeField] private CharacterMovement movement;
        [Tooltip("Screen-space horizontal speed share below which the facing does not change.")]
        [SerializeField] private float flipThreshold = 0.1f;
        [Header("Attack feedback")]
        [SerializeField] private float windupLean = 9f;
        [SerializeField] private float lungeLean = 12f;
        [SerializeField] private float lungeDistance = 0.15f;
        [SerializeField] private float lungeDuration = 0.15f;
        [Header("Statuses")]
        [SerializeField] private Color stunTint = new(1f, 0.9f, 0.35f);
        [SerializeField] private Color rootTint = new(0.55f, 1f, 0.55f);
        [SerializeField] private Color silenceTint = new(0.8f, 0.55f, 1f);
        [SerializeField] private Color slowTint = new(0.55f, 0.8f, 1f);
        [SerializeField] private Color burnTint = new(1f, 0.6f, 0.35f);
        [SerializeField] private Color bleedTint = new(1f, 0.5f, 0.5f);
        [SerializeField] private float stunShake = 4f;
        [Header("Hit")]
        [SerializeField] private Color hitTint = new(1f, 0.45f, 0.45f);
        [SerializeField] private float hitDuration = 0.12f;

        private SpriteRenderer _renderer;
        private Transform _camera;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private float _windupStart = -1f, _windupEnd = -1f, _impactTime = -1f;
        private bool _stunned;
        private Color _statusColor = Color.white;
        private float _hitTime = -1f;

        public SpriteRenderer Renderer => _renderer;
        /// <summary>Tint of the current status (the hit flash plays on top of it).</summary>
        public Color StatusColor => _statusColor;
        public bool FacesLeft => _renderer.flipX;

        [Inject]
        public void Construct(Camera mainCamera) {
            _camera = mainCamera.transform;
        }

        private void Awake() {
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
        }

        /// <summary>Took a hit: short red flash and squash (feedback, §6 step 7).</summary>
        public void PlayHit() => _hitTime = Time.time;

        /// <summary>Basic attack windup of <paramref name="duration"/> seconds starts now.</summary>
        public void PlayWindup(float duration) {
            _windupStart = Time.time;
            _windupEnd = Time.time + Mathf.Max(0.01f, duration);
            _impactTime = -1f;
        }

        /// <summary>The attack lands / fires: quick lunge forward.</summary>
        public void PlayImpact() {
            _windupStart = _windupEnd = -1f;
            _impactTime = Time.time;
        }

        /// <summary>Windup cancelled: back to rest.</summary>
        public void StopAttack() {
            _windupStart = _windupEnd = _impactTime = -1f;
        }

        public void SetSprite(Sprite sprite) => _renderer.sprite = sprite;

        /// <summary>Tint of the strongest status, white when none.</summary>
        public void SetStatus(StatusEffects status) {
            _stunned = status.IsStunned;
            if (_stunned) StopAttack();
            _statusColor = status.IsStunned ? stunTint
                : status.IsRooted ? rootTint
                : status.IsSilenced ? silenceTint
                : status.IsSlowed ? slowTint
                : status.IsBurning ? burnTint
                : status.IsBleeding ? bleedTint
                : Color.white;
        }

        /// <summary>Turns the sprite towards a world direction (attacking while standing still).</summary>
        public void Face(Vector3 worldDirection) {
            if (_camera == null) return;
            float side = Vector3.Dot(worldDirection, _camera.right);
            if (Mathf.Abs(side) > 0.01f) _renderer.flipX = side < 0f;
        }

        // TODO(phase 6): hundreds of enemies → one central billboard update instead of one LateUpdate each.
        private void LateUpdate() {
            if (_camera == null) return;

            if (movement != null) {
                float side = Vector3.Dot(movement.MoveDirection, _camera.right);
                if (Mathf.Abs(side) > flipThreshold) _renderer.flipX = side < 0f;
            }

            // Pivot at the feet: tilting and squashing keep the feet on the ground
            float facing = _renderer.flipX ? -1f : 1f;
            float tilt = 0f, squash = 0f, lunge = 0f;
            if (_windupStart >= 0f) {
                float p = Mathf.Clamp01((Time.time - _windupStart) / (_windupEnd - _windupStart));
                tilt = windupLean * p * facing;                      // leans back
                squash = 0.08f * p;
            }
            else if (_impactTime >= 0f) {
                float q = 1f - (Time.time - _impactTime) / lungeDuration;
                if (q <= 0f) _impactTime = -1f;
                else {
                    tilt = -lungeLean * q * facing;                  // lunges forward
                    squash = -0.1f * q;
                    lunge = lungeDistance * q * facing;
                }
            }
            if (_stunned) tilt += Mathf.Sin(Time.time * 40f) * stunShake;   // dazed
            float hit = _hitTime >= 0f ? 1f - (Time.time - _hitTime) / hitDuration : 0f;
            if (hit <= 0f) {
                _hitTime = -1f;
                hit = 0f;
            }
            _renderer.color = Color.Lerp(_statusColor, hitTint, hit);
            squash -= 0.12f * hit;   // squashed by the blow
            transform.rotation = _camera.rotation * Quaternion.Euler(0f, 0f, tilt);
            transform.localScale = new Vector3(_baseScale.x * (1f - squash), _baseScale.y * (1f + squash * 0.75f), _baseScale.z);
            transform.position = transform.parent.TransformPoint(_basePosition) + _camera.right * lunge;
        }
    }
}
