using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Train.Navigation {
    public class Navigator : MonoBehaviour {
        [Header("Settings")]
        [SerializeField] private Transform targetTransform;
        [SerializeField] private NavMeshSurface navMeshSurface;
        [SerializeField] private float updateInterval = 0.1f;

        private Vector3[] WorldPathArray { get; set; }
        private Vector3[] GetPathArray() => WorldPathArray ?? Array.Empty<Vector3>();

        private NavMeshPath _navPath;
        private float _timer;

        private void Awake() => _navPath = new NavMeshPath();

        private void Start() => UpdatePathData();

        private void FixedUpdate() {
            if (!targetTransform) {
                return;
            }

            _timer += Time.fixedDeltaTime;
            if (!(_timer >= updateInterval)) {
                return;
            }

            ExecuteNavigation();
            DrawPathDebug();
            _timer = 0f;
        }

        private void ExecuteNavigation() {
            if (!navMeshSurface) {
                return;
            }

            navMeshSurface.BuildNavMesh();

            UpdatePathData();
        }

        private void UpdatePathData() {
            if (NavMesh.CalculatePath(transform.position, targetTransform.position, NavMesh.AllAreas, _navPath)) {
                WorldPathArray = _navPath.corners;
            } else {
                WorldPathArray = Array.Empty<Vector3>();
            }
        }

        private void DrawPathDebug() {
            if (WorldPathArray == null || WorldPathArray.Length < 2) {
                return;
            }

            for (int i = 0; i < WorldPathArray.Length - 1; i++) {
                Debug.DrawLine(WorldPathArray[i], WorldPathArray[i + 1], Color.green, updateInterval);
            }
        }


        public Vector3[] GetUpdatedPath() {
            UpdatePathData();
            return GetPathArray();
        }
    }
}
