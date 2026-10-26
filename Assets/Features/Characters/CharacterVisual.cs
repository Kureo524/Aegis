using Features.Combat;
using Features.Events;
using Features.Movement;
using Features.Spells;
using Systems.EventBusses;
using UnityEngine;
using VContainer;

namespace Features.Characters {
    /// <summary>
    /// 2.5D look of a character (design doc §3): a sprite on a child of the physics root that always
    /// faces the camera (billboard: same rotation as the camera, so it reads flat on screen with no
    /// perspective skew), flipped left / right with the horizontal movement on screen.
    /// Sprites are drawn facing right.
    ///
    /// With a <see cref="SpriteAnimationSet"/> (frames pre-rendered from the 3D model) it plays idle /
    /// run (paced on the actual speed), the dash, the attack timed so its impact frame lands at the
    /// end of the windup, the clip of each spell cast, the hit reaction and, on death, hands the death
    /// clip to DeathAnimations (the body itself goes back to its pool at once). Without one, the
    /// still sprite leans back during the windup and lunges at the impact.
    ///
    /// Statuses (§5.7) tint the sprite (stun yellow and shaking, root green, silence purple, slow
    /// blue, burn orange, bleed red) so a CC reads at a glance, on hundreds of enemies too. A hit
    /// flashes it red and squashes it.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterVisual : MonoBehaviour {
        [SerializeField] private CharacterMovement movement;
        [Tooltip("Screen-space horizontal speed share below which the facing does not change.")]
        [SerializeField] private float flipThreshold = 0.1f;
        [Header("Attack feedback (still sprites)")]
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
        private IEventBus _eventBus;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private float _windupStart = -1f, _windupEnd = -1f, _impactTime = -1f;
        private bool _stunned;
        private Color _statusColor = Color.white;
        private float _hitTime = -1f;

        private SpriteAnimationSet _animations;
        private SpriteClip _action;        // attack, cast or hit, played once over the loops
        private float _actionStart, _actionLength;
        private bool _actionInterruptible;
        private float _loopTime;

        public SpriteRenderer Renderer => _renderer;
        /// <summary>Tint of the current status (the hit flash plays on top of it).</summary>
        public Color StatusColor => _statusColor;
        public bool FacesLeft => _renderer.flipX;
        public bool IsAnimated => _animations != null;
        /// <summary>Clip played over the idle / run loop right now, null when none.</summary>
        public SpriteClip CurrentAction => _action;

        [Inject]
        public void Construct(Camera mainCamera, IEventBus eventBus) {
            _camera = mainCamera.transform;
            _eventBus = eventBus;
        }

        private void Awake() {
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
        }

        /// <summary>The still sprite and, when given, the animations that replace it.</summary>
        public void SetLook(Sprite sprite, SpriteAnimationSet animations) {
            _animations = animations;
            _action = null;
            _renderer.sprite = animations != null && animations.Still != null ? animations.Still : sprite;
        }

        public void SetSprite(Sprite sprite) => SetLook(sprite, null);

        /// <summary>Took a hit: short red flash and squash (feedback, §6 step 7), plus the hit clip if any.</summary>
        public void PlayHit() {
            _hitTime = Time.time;
            if (_animations != null && !_animations.Hit.IsEmpty && _action == null)
                PlayAction(_animations.Hit, _animations.Hit.Duration, true);
        }

        /// <summary>Basic attack windup of <paramref name="duration"/> seconds starts now.</summary>
        public void PlayWindup(float duration) {
            _windupStart = Time.time;
            _windupEnd = Time.time + Mathf.Max(0.01f, duration);
            _impactTime = -1f;
            if (_animations != null && !_animations.Attack.IsEmpty)
                // The whole clip is stretched so that its impact frame lands at the end of the windup
                PlayAction(_animations.Attack, Mathf.Max(0.01f, duration) / _animations.AttackImpact, true);
        }

        /// <summary>The attack lands / fires: quick lunge forward (still sprites).</summary>
        public void PlayImpact() {
            _windupStart = _windupEnd = -1f;
            _impactTime = Time.time;
        }

        /// <summary>Windup cancelled: back to rest.</summary>
        public void StopAttack() {
            _windupStart = _windupEnd = _impactTime = -1f;
            if (_animations != null && _action == _animations.Attack) _action = null;
        }

        /// <summary>A spell is being cast: its clip at its own pace (at least the cast time).</summary>
        public void PlayCast(SpellDefinition spell, float castTime) {
            if (_animations == null) return;
            SpriteClip clip = _animations.CastClip(spell);
            if (clip.IsEmpty) return;
            PlayAction(clip, Mathf.Max(castTime, Mathf.Min(clip.Duration, 0.9f)), false);
        }

        /// <summary>The character died: its death clip plays on its own while the body goes away.</summary>
        public void PlayDeath() {
            _action = null;
            if (_animations == null || _animations.Death.IsEmpty || _eventBus == null) return;
            _eventBus.Publish(new DeathAnimationEvent(_animations.Death, transform.parent.position, _renderer.flipX, transform.lossyScale));
        }

        private void PlayAction(SpriteClip clip, float length, bool interruptible) {
            _action = clip;
            _actionStart = Time.time;
            _actionLength = Mathf.Max(0.01f, length);
            _actionInterruptible = interruptible;
        }

        private bool _elite;
        private const float EliteScale = 1.2f;
        private static readonly Color EliteTint = new(0.86f, 0.82f, 1f);

        /// <summary>Elites of the horde (they carry items): bigger, a cold aurora tint.</summary>
        public void SetElite(bool elite) => _elite = elite;

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

        // Animation frame: an action over everything, else dash, run or idle
        private void Animate(bool moving, float speed) {
            if (_action != null) {
                float t = (Time.time - _actionStart) / _actionLength;
                // Walking cuts an attack recovery or a hit reaction, never a cast
                bool cut = _actionInterruptible && moving && (_action != _animations.Attack || _windupStart < 0f);
                if (t >= 1f || cut || _stunned) _action = null;
                else {
                    _renderer.sprite = _action.FrameAt(t);
                    return;
                }
            }
            SpriteClip loop;
            float pace = 1f;
            if (movement != null && movement.IsDashing && !_animations.Dash.IsEmpty) loop = _animations.Dash;
            else if (moving && !_animations.Run.IsEmpty) {
                loop = _animations.Run;
                pace = Mathf.Clamp(speed / _animations.RunReferenceSpeed, 0.5f, 2f);
            }
            else loop = _animations.Idle;
            if (_stunned) pace = 0f;   // frozen in place
            _loopTime += Time.deltaTime * pace;
            if (!loop.IsEmpty) _renderer.sprite = loop.At(_loopTime);
        }

        // TODO(phase 6): hundreds of enemies → one central billboard update instead of one LateUpdate each.
        private void LateUpdate() {
            if (_camera == null) return;

            bool moving = false;
            float speed = 0f;
            if (movement != null) {
                float side = Vector3.Dot(movement.MoveDirection, _camera.right);
                if (Mathf.Abs(side) > flipThreshold) _renderer.flipX = side < 0f;
                speed = movement.CurrentSpeed;
                moving = movement.IsMoving && speed > 0.2f;
            }
            if (_animations != null) Animate(moving, speed);

            // Pivot at the feet: tilting and squashing keep the feet on the ground
            float facing = _renderer.flipX ? -1f : 1f;
            float tilt = 0f, squash = 0f, lunge = 0f;
            if (_animations == null) {
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
            }
            else if (_windupStart >= 0f && Time.time > _windupEnd + 0.05f) _windupStart = -1f;
            if (_stunned) tilt += Mathf.Sin(Time.time * 40f) * stunShake;   // dazed
            float hit = _hitTime >= 0f ? 1f - (Time.time - _hitTime) / hitDuration : 0f;
            if (hit <= 0f) {
                _hitTime = -1f;
                hit = 0f;
            }
            Color baseColor = _elite && _statusColor == Color.white ? EliteTint : _statusColor;
            _renderer.color = Color.Lerp(baseColor, hitTint, hit);
            squash -= 0.12f * hit;   // squashed by the blow
            transform.rotation = _camera.rotation * Quaternion.Euler(0f, 0f, tilt);
            float size = _elite ? EliteScale : 1f;
            transform.localScale = new Vector3(_baseScale.x * size * (1f - squash), _baseScale.y * size * (1f + squash * 0.75f), _baseScale.z * size);
            transform.position = transform.parent.TransformPoint(_basePosition) + _camera.right * lunge;
        }
    }
}
