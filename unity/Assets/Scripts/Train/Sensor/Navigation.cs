using System;
using Unity.AI.Navigation;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

namespace Train.Sensor {
    public class Navigation : SensorComponent {
        public Transform targetTransform;
        private NavMeshPath _navMeshPath;
        private NavMeshSurface _navMeshSurface;
        private GameObject _navMeshObject;
        [SerializeField] private LayerMask targetLayer;

        private NavigationSensor _navigationSensor;

        public Vector3[] Corners => _navMeshPath != null ? _navMeshPath.corners : Array.Empty<Vector3>();

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
            Reset();
        }

        public void Reset() => _navMeshSurface.BuildNavMesh();

        private void Update() {
            if (!targetTransform) {
                return;
            }

            NavMesh.CalculatePath(transform.position, targetTransform.position, NavMesh.AllAreas, _navMeshPath);
        }

        private void OnDrawGizmos() {
            if (Corners.Length == 0) {
                return;
            }

            Gizmos.color = Color.red;

            for (int i = 0; i < Corners.Length - 1; i++) {
                Gizmos.DrawSphere(Corners[i], 0.05f);
                Gizmos.DrawLine(Corners[i], Corners[i + 1]);
            }

            Gizmos.DrawSphere(Corners[^1], 0.1f);
        }
    }
}
