using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Train.Navigation {
    public class Navigator : MonoBehaviour {
        [SerializeField] private Transform targetTransform;
        [SerializeField] private Transform agentTransform;
        private NavMeshSurface _navMeshSurface;
        private NavMeshPath _navMeshPath;

        public Vector3[] Corners => _navMeshPath != null ? _navMeshPath.corners : Array.Empty<Vector3>();

        private void Awake() {
            _navMeshPath = new NavMeshPath();
            _navMeshSurface = GetComponent<NavMeshSurface>();
            _navMeshSurface.BuildNavMesh();
        }


        private void Update() {
            if (!targetTransform && !agentTransform) {
                return;
            }

            NavMesh.CalculatePath(agentTransform.position, targetTransform.position, NavMesh.AllAreas, _navMeshPath);
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
