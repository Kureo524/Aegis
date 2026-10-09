using UnityEngine;
using UnityEngine.AI;

namespace Systems.Characters {
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class CharacterMovement : MonoBehaviour {
        [SerializeField] public float moveSpeed = 5f;
        [SerializeField] public float stopDistance = 0.1f;
        [SerializeField] public float rotationSpeed = 15f;

        private Rigidbody _rb;
        private NavMeshAgent _agent;
        private Vector3 _destination;
        private bool _hasDestination;
        private NavMeshPath _path;
        private int _currentCorner;

        public bool IsMoving => _hasDestination;
        public Vector3 Destination => _destination;

        private void Awake() {
            _rb = GetComponent<Rigidbody>();
            _agent = GetComponent<NavMeshAgent>();
            _path = new NavMeshPath();
            
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.speed = moveSpeed;
            _agent.stoppingDistance = stopDistance;
            _agent.acceleration = 1000f;
            _agent.autoBraking = false;
            _agent.autoRepath = false;
        }

        private void Start() {
            StartCoroutine(InitializeNavMesh());
        }

        private System.Collections.IEnumerator InitializeNavMesh() {
            yield return null;
            
            if (NavMesh.SamplePosition(_rb.position, out NavMeshHit hit, 10f, NavMesh.AllAreas)) {
                _agent.Warp(hit.position);
            }
        }

        public void SetDestination(Vector3 point) {
            _destination = point;
            _hasDestination = true;
            
            // Snap to NavMesh
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 10f, NavMesh.AllAreas)) {
                point = hit.position;
                _destination = point;
            }
            
            // Calculate path synchronously (avoids Unity 6 pending bug)
            if (_agent.isOnNavMesh && NavMesh.CalculatePath(_rb.position, point, NavMesh.AllAreas, _path)) {
                _currentCorner = 0;
                _agent.SetPath(_path);
            } else {
                // Fallback: direct movement
                _path.ClearCorners();
            }
        }

        public void Stop() {
            _hasDestination = false;
            _agent.ResetPath();
            _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        }

        private void FixedUpdate() {
            if (!_hasDestination) return;

            if (_agent.isOnNavMesh && _path.corners.Length > 0) {
                // Move along path corners
                if (_currentCorner >= _path.corners.Length) {
                    Stop();
                    return;
                }

                Vector3 targetCorner = _path.corners[_currentCorner];
                Vector3 toCorner = targetCorner - _rb.position;
                toCorner.y = 0;
                float distance = toCorner.magnitude;

                if (distance <= stopDistance) {
                    _currentCorner++;
                    if (_currentCorner >= _path.corners.Length) {
                        Stop();
                        return;
                    }
                }

                Vector3 direction = toCorner.normalized;
                _rb.linearVelocity = direction * moveSpeed;
            } else {
                // Fallback: direct movement
                Vector3 toDestination = _destination - _rb.position;
                toDestination.y = 0;
                float distance = toDestination.magnitude;

                if (distance <= stopDistance) {
                    Stop();
                    return;
                }

                Vector3 direction = toDestination.normalized;
                _rb.linearVelocity = direction * moveSpeed;
            }
        }

        private void Update() {
            if (!_hasDestination) return;

            Vector3 direction;
            
            if (_agent.isOnNavMesh && _path.corners.Length > 0 && _currentCorner < _path.corners.Length) {
                Vector3 targetCorner = _path.corners[_currentCorner];
                direction = (targetCorner - _rb.position).normalized;
            } else {
                Vector3 toDestination = _destination - _rb.position;
                toDestination.y = 0;
                float distance = toDestination.magnitude;

                if (distance <= stopDistance) {
                    Stop();
                    return;
                }
                direction = toDestination.normalized;
            }
            
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f) {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
}
