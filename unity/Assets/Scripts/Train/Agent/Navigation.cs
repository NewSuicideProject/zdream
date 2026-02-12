using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Train.Agent {
    [RequireComponent(typeof(Proprioception), typeof(NavMeshSurface))]
    public class Navigation : MonoBehaviour {
        [SerializeField] private Transform targetTransform;
        private NavMeshPath _navMeshPath;
        private NavMeshSurface _navMeshSurface;
        private Proprioception _proprioception;

        public Vector3[] Corners => _navMeshPath != null ? _navMeshPath.corners : Array.Empty<Vector3>();

        private void Awake() {
            _navMeshPath = new NavMeshPath();
            _navMeshSurface = GetComponent<NavMeshSurface>();
            _proprioception = GetComponent<Proprioception>();
            Reset();
        }

        public void Reset() => _navMeshSurface.BuildNavMesh();


        private void Update() {
            if (!targetTransform) {
                return;
            }

            NavMesh.CalculatePath(_proprioception.Position, targetTransform.position, NavMesh.AllAreas, _navMeshPath);
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
