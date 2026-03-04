using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

namespace Train.Sensor {
    public struct Corner {
        public Vector3 RelativePosition;
        public Vector3 Direction;
    }

    public class Navigation : SensorComponent {
        public Transform targetTransform;
        private NavMeshPath _navMeshPath;
        private NavMeshSurface _navMeshSurface;
        private GameObject _navMeshObject;
        [SerializeField] private LayerMask targetLayer;

        private NavigationSensor _navigationSensor;

        public readonly List<Corner> Corners = new();

        public override ISensor[] CreateSensors() {
            _navigationSensor = new NavigationSensor(this);
            return new ISensor[] { _navigationSensor };
        }

        private void Awake() {
            _navMeshObject = new GameObject("NavMeshSurface");
            DontDestroyOnLoad(_navMeshObject);
            _navMeshSurface = _navMeshObject.AddComponent<NavMeshSurface>();
            _navMeshSurface.layerMask = targetLayer;
            _navMeshPath = new NavMeshPath();
            OnEpisodeBegin();
        }

        public void OnEpisodeBegin() => _navMeshSurface.BuildNavMesh();

        private void FixedUpdate() {
            if (!targetTransform) {
                return;
            }

            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = Vector3.forward;
            } else {
                projectedForward.Normalize();
            }

            Quaternion inverseYaw = Quaternion.Inverse(Quaternion.LookRotation(projectedForward, Vector3.up));

            Corners.Clear();
            NavMesh.CalculatePath(transform.position, targetTransform.position, NavMesh.AllAreas, _navMeshPath);
            for (int i = 1; i < _navMeshPath.corners.Length - 1; i++) {
                Vector3 corner = _navMeshPath.corners[i];
                Vector3 nextCorner = _navMeshPath.corners[i + 1];

                Corners.Add(new Corner {
                    RelativePosition = inverseYaw * (corner - transform.position),
                    Direction = inverseYaw * (nextCorner - corner).normalized
                });
            }
        }

        private void OnDrawGizmos() {
            if (_navMeshPath == null) {
                return;
            }

            Vector3[] corners = _navMeshPath.corners;

            if (corners.Length == 0) {
                return;
            }

            Gizmos.color = Color.red;

            for (int i = 0; i < corners.Length - 1; i++) {
                Gizmos.DrawSphere(corners[i], 0.05f);
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }

            Gizmos.DrawSphere(corners[^1], 0.1f);
        }
    }
}
