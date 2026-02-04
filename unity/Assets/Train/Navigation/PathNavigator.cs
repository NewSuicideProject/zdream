using System;
using UnityEngine;
using UnityEngine.AI;

namespace Train.Navigation {
    public class PathNavigator : MonoBehaviour {
        [Header("Settings")] [SerializeField] private Transform targetTransform;
        [SerializeField] private float updateInterval = 0.1f;

        //[Header("Output Data")] public Vector3 FinalTargetPosition { get; private set; }
        private Vector3[] WorldPathArray { get; set; }
        private Vector3[] GetPathArray() => WorldPathArray ?? Array.Empty<Vector3>();

        private NavMeshPath _navPath;
        private float _timer;

        private void Awake() => _navPath = new NavMeshPath();

        private void Start() => UpdatePathData();

        private void Update() {
            if (!targetTransform) {
                return;
            }

            _timer += Time.deltaTime;
            if (!(_timer >= updateInterval)) {
                return;
            }

            DrawPathDebug();
            _timer = 0f;
        }

        private void UpdatePathData() {
            if (NavMesh.CalculatePath(transform.position, targetTransform.position, NavMesh.AllAreas, _navPath)) {
                //FinalTargetPosition = targetTransform.position;
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

        public Vector3[] GetPathDeltas() {
            if (WorldPathArray == null || WorldPathArray.Length < 2) {
                return Array.Empty<Vector3>();
            }

            int vectorCount = WorldPathArray.Length - 1;
            Vector3[] pathVectors = new Vector3[vectorCount];

            for (int i = 0; i < vectorCount; i++) {
                pathVectors[i] = WorldPathArray[i + 1] - WorldPathArray[i];
            }

            return pathVectors;
        }
    }
}
