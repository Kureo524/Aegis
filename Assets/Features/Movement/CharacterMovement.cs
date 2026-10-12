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
    /// Speed is the owner's MoveSpeed stat (<see cref="Init"/>): same code for everyone,
    /// different speed. Does not move before Init.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class CharacterMovement : MonoBehaviour {
        [SerializeField] public float stopDistance = 0.1f;
        [SerializeField] public float rotationSpeed = 15f;

        private const float SampleRadius = 10f;

        private Rigidbody _rb;
        private NavMeshAgent _agent;
        private NavMeshPath _path;
        private Vector3[] _corners = System.Array.Empty<Vector3>();   // cached: NavMeshPath.corners allocates
        private Vector3 _destination;
        private bool _hasDestination;
        private int _currentCorner;
        private Vector3 _moveDirection;
        private Stat _moveSpeed;

        private float MoveSpeed => _moveSpeed?.Value ?? 0f;

        public bool IsMoving => _hasDestination;
        public Vector3 Destination => _destination;
        /// <summary>Normalized horizontal direction of the current move, zero when idle.</summary>
        public Vector3 MoveDirection => _hasDestination ? _moveDirection : Vector3.zero;

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
            if (NavMesh.SamplePosition(_rb.position, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas)) {
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
            _currentCorner = 0;

            bool hasPath = _agent.isOnNavMesh && NavMesh.CalculatePath(_rb.position, point, NavMesh.AllAreas, _path);
            _corners = hasPath ? _path.corners : System.Array.Empty<Vector3>();   // no path: straight line fallback
        }

        public void Stop() {
            _hasDestination = false;
            _moveDirection = Vector3.zero;
            _corners = System.Array.Empty<Vector3>();
            SetHorizontalVelocity(Vector3.zero);
        }

        private void FixedUpdate() {
            if (_agent.isOnNavMesh) _agent.nextPosition = _rb.position;
            if (!_hasDestination) return;

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

        // Keeps the vertical component so gravity keeps working.
        private void SetHorizontalVelocity(Vector3 velocity) {
            velocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocity;
        }
    }
}
