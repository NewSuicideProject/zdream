using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Train.Navigation {
    public class Navigator : MonoBehaviour {
        [SerializeField] private Transform targetTransform;
        private NavMeshSurface _navMeshSurface;

        private Vector3[] Corners { get; set; }
        public Vector3[] GetCorners() => Corners ?? Array.Empty<Vector3>();

        private NavMeshPath _navMeshPath;

        private void Awake() {
            _navMeshPath = new NavMeshPath();
            _navMeshSurface = GetComponent<NavMeshSurface>();
        }


        private void Update() {
            if (!targetTransform) {
                return;
            }

            Navigate();
        }

        private void Navigate() {
            if (!_navMeshSurface) {
                return;
            }

            _navMeshSurface.BuildNavMesh();

            if (NavMesh.CalculatePath(transform.position, targetTransform.position, NavMesh.AllAreas, _navMeshPath)) {
                Corners = _navMeshPath.corners;
            } else {
                Corners = Array.Empty<Vector3>();
            }
        }


        private void OnDrawGizmos() {
            if (Corners == null || Corners.Length < 2) {
                return;
            }

            Gizmos.color = Color.red;

            for (int i = 0; i < Corners.Length - 1; i++) {
                Gizmos.DrawLine(Corners[i], Corners[i + 1]);
                Gizmos.DrawSphere(Corners[i], 0.1f);
            }

            Gizmos.DrawSphere(Corners[^1], 0.1f);
        }
    }
}
