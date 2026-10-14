using Features.Stats;
using UnityEngine;
using UnityEngine.AI;

namespace Features.Movement {
    /// <summary>
    /// Moves the Rigidbody to a point along a NavMesh path (design doc §4.4). Does not know who
    /// gives the order (player, AI…).
    ///
    /// The path is computed synchronously with NavMesh.CalculatePath and its corners are followed
    /// by hand; the NavMeshAgent never moves the body (updatePosition = false) and is only kept in
    /// sync with it, so it stays usable for queries and avoidance later.
    ///
    /// <see cref="Steer"/> is the other way to move, for the hordes (§9.6): walk in a direction given
    /// from outside (the flow field), no path at all.
    ///
    /// Speed is the owner's MoveSpeed stat (<see cref="Init"/>): same code for everyone,
    /// different speed. Does not move before Init.
    ///
    /// <see cref="Immobilized"/> (stun, root, §5.7) holds the body in place without forgetting the
    /// order: the walk resumes when it ends. A dash in progress is stopped.
    ///
    /// Steering, dashes and bodies pushing each other ignore the NavMesh: after every physics step
    /// the body is pulled back onto it (the agent, synced with the body, is constrained to the
    /// NavMesh), so nothing walks, charges or is pushed off the map. A body that still falls under
    /// the ground is put back on the closest walkable point.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class CharacterMovement : MonoBehaviour {
        [SerializeField] public float stopDistance = 0.1f;
        [SerializeField] public float rotationSpeed = 15f;

        private const float SampleRadius = 10f;
        private const float OffMeshTolerance = 0.05f;
        private const float FallLimit = -3f;

        private Rigidbody _rb;
        private NavMeshAgent _agent;
        private NavMeshPath _path;
        private Vector3[] _corners = System.Array.Empty<Vector3>();   // cached: NavMeshPath.corners allocates
        private Vector3 _destination;
        private bool _hasDestination;
        private bool _steering;
        private float _dashUntil = -1f;
        private float _dashSpeed;
        private int _currentCorner;
        private Vector3 _moveDirection;
        private Stat _moveSpeed;

        private float MoveSpeed => _moveSpeed?.Value ?? 0f;

        public bool IsMoving => _hasDestination;
        /// <summary>Stunned or rooted: no move, orders are kept for later.</summary>
        public bool Immobilized { get; set; }
        public Vector3 Destination => _destination;
        /// <summary>Normalized horizontal direction of the current move, zero when idle.</summary>
        public Vector3 MoveDirection => _hasDestination ? _moveDirection : Vector3.zero;
        /// <summary>Horizontal speed of the body (run animation pace).</summary>
        public float CurrentSpeed {
            get {
                if (_rb == null) return 0f;
                Vector3 v = _rb.linearVelocity;
                v.y = 0f;
                return v.magnitude;
            }
        }

        private void Awake() {
            _rb = GetComponent<Rigidbody>();
            // The body moves at the physics rate while the camera follows it every rendered frame:
            // without interpolation the character visibly stutters (design doc §3).
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _agent = GetComponent<NavMeshAgent>();
            _path = new NavMeshPath();

            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.stoppingDistance = stopDistance;
            _agent.acceleration = 1000f;
            _agent.autoBraking = false;
            _agent.autoRepath = false;
        }

        private void Start() {
            if (_agent.enabled && NavMesh.SamplePosition(_rb.position, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas)) {
                _agent.Warp(hit.position);
            }
        }

        public void Init(Stat moveSpeed) {
            if (_moveSpeed != null) _moveSpeed.Changed -= OnMoveSpeedChanged;
            _moveSpeed = moveSpeed;
            if (_moveSpeed != null) _moveSpeed.Changed += OnMoveSpeedChanged;
            OnMoveSpeedChanged(MoveSpeed);
        }

        private void OnMoveSpeedChanged(float speed) => _agent.speed = speed;

        public void SetDestination(Vector3 point) {
            // Clicks outside the NavMesh go to the closest walkable point
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas)) {
                point = hit.position;
            }

            _destination = point;
            _hasDestination = true;
            _steering = false;
            _currentCorner = 0;

            bool hasPath = _agent.isOnNavMesh && NavMesh.CalculatePath(_rb.position, point, NavMesh.AllAreas, _path);
            _corners = hasPath ? _path.corners : System.Array.Empty<Vector3>();   // no path: straight line fallback
        }

        /// <summary>Walks in <paramref name="direction"/> (horizontal, normalized) until the next order.</summary>
        public void Steer(Vector3 direction) {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) {
                Stop();
                return;
            }
            _steering = true;
            _hasDestination = true;
            _moveDirection = direction.normalized;
            _corners = System.Array.Empty<Vector3>();
        }

        /// <summary>Straight dash (boss charge): <paramref name="speed"/> for <paramref name="duration"/> seconds, ignoring MoveSpeed.</summary>
        public void Dash(Vector3 direction, float speed, float duration) {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || duration <= 0f) return;
            Stop();
            _moveDirection = direction.normalized;
            _dashSpeed = speed;
            _dashUntil = Time.time + duration;
            _hasDestination = true;
        }

        public bool IsDashing => _dashUntil > Time.time;

        /// <summary>Instant move (blink): no path, keeps physics and agent in sync.</summary>
        public void Teleport(Vector3 position) {
            Stop();
            _rb.position = position;
            transform.position = position;
            if (_agent.enabled) _agent.Warp(position);
        }

        public void Stop() {
            _hasDestination = false;
            _steering = false;
            _dashUntil = -1f;
            _moveDirection = Vector3.zero;
            _corners = System.Array.Empty<Vector3>();
            SetHorizontalVelocity(Vector3.zero);
        }

        private void FixedUpdate() {
            KeepOnNavMesh();
            if (!_hasDestination) return;

            if (Immobilized) {
                if (_dashUntil > 0f) Stop();
                else SetHorizontalVelocity(Vector3.zero);
                return;
            }

            if (_dashUntil > 0f) {
                if (Time.time >= _dashUntil) {
                    Stop();
                    return;
                }
                SetHorizontalVelocity(_moveDirection * _dashSpeed);
                FaceMoveDirection();
                return;
            }

            if (_steering) {
                SetHorizontalVelocity(_moveDirection * MoveSpeed);
                FaceMoveDirection();
                return;
            }

            if (!TryGetNextWaypoint(out Vector3 waypoint)) {
                Stop();
                return;
            }

            Vector3 toWaypoint = waypoint - _rb.position;
            toWaypoint.y = 0f;
            _moveDirection = toWaypoint.normalized;
            SetHorizontalVelocity(_moveDirection * MoveSpeed);
            FaceMoveDirection();
        }

        // The agent follows the body but cannot leave the NavMesh: where they part, the body went off it.
        private void KeepOnNavMesh() {
            if (_rb.isKinematic) return;
            if (_rb.position.y < FallLimit) {
                Recover();
                return;
            }
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.nextPosition = _rb.position;
            Vector3 onMesh = _agent.nextPosition;
            Vector3 back = onMesh - _rb.position;
            back.y = 0f;
            if (back.sqrMagnitude <= OffMeshTolerance * OffMeshTolerance) return;
            _rb.position = new Vector3(onMesh.x, _rb.position.y, onMesh.z);
            Vector3 normal = back.normalized;
            Vector3 velocity = _rb.linearVelocity;
            float outwards = -Vector3.Dot(velocity, normal);
            if (outwards > 0f) _rb.linearVelocity = velocity + normal * outwards;   // no more speed off the edge
        }

        private void Recover() {
            Vector3 probe = new Vector3(_rb.position.x, 0f, _rb.position.z);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 200f, NavMesh.AllAreas)) return;
            _rb.linearVelocity = Vector3.zero;
            Teleport(hit.position);
        }

        // Rotated through the Rigidbody, in FixedUpdate: writing transform.rotation directly
        // would bypass (and break) the interpolation.
        private void FaceMoveDirection() {
            if (_moveDirection.sqrMagnitude < 0.001f) return;
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed));
        }

        // Skips the corners already reached. False once the destination itself is reached.
        private bool TryGetNextWaypoint(out Vector3 waypoint) {
            Vector3[] corners = _corners;

            if (corners.Length == 0) {
                waypoint = _destination;
                return HorizontalDistance(_destination) > stopDistance;
            }

            while (_currentCorner < corners.Length && HorizontalDistance(corners[_currentCorner]) <= stopDistance) {
                _currentCorner++;
            }

            waypoint = _currentCorner < corners.Length ? corners[_currentCorner] : _destination;
            return _currentCorner < corners.Length;
        }

        private float HorizontalDistance(Vector3 point) {
            Vector3 delta = point - _rb.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        // Keeps the vertical component so gravity keeps working. A dead body is kinematic: no velocity.
        private void SetHorizontalVelocity(Vector3 velocity) {
            if (_rb.isKinematic) return;
            velocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocity;
        }
    }
}
