using System.Collections.Generic;
using UnityEngine;

namespace Train.Sensors.Navigation {
    public class NavigationSensor : MonoBehaviour {
        [Header("References")] [SerializeField]
        private Transform rootTransform; // The agent's root body

        [Header("Settings")] [SerializeField] private int sampleCount = 10; // Corresponds to max_t
        [SerializeField] private float sampleInterval = 2.0f; // Distance between samples in meters

        [Header("Debug")] [SerializeField] [ReadOnly]
        private float[] navigationBuffer;

        public float[] NavigationBuffer => navigationBuffer;

        private void Awake() {
            if (rootTransform == null) {
                rootTransform = transform;
            }

            // Initialize buffer: sampleCount * 5 features (pos_x, pos_y, pos_z, fwd_x, fwd_z)
            navigationBuffer = new float[sampleCount * 5];
        }

        /// <summary>
        /// Updates the navigation buffer with local-space path data.
        /// Should be called before CollectObservations.
        /// </summary>
        /// <param name="pathPoints">The world-space points of the current path</param>
        /// <param name="currentWaypointIndex"></param>
        public void UpdateNavigationData(List<Vector3> pathPoints, int currentWaypointIndex) {
            if (pathPoints == null || pathPoints.Count == 0) {
                return;
            }

            int bufferIndex = 0;
            Vector3 lastValidPoint = rootTransform.position;

            for (int i = 0; i < sampleCount; i++) {
                int targetIdx = currentWaypointIndex + i;
                Vector3 worldPos;
                Vector3 worldForward = default;

                // 1. Get World Position and Forward
                if (targetIdx < pathPoints.Count) {
                    worldPos = pathPoints[targetIdx];

                    // Calculate forward toward the next point, or use previous direction at the end
                    if (targetIdx + 1 < pathPoints.Count) {
                        worldForward = (pathPoints[targetIdx + 1] - pathPoints[targetIdx]).normalized;
                    } else {
                        if (rootTransform != null) {
                            worldForward = targetIdx > 0
                                ? (pathPoints[targetIdx] - pathPoints[targetIdx - 1]).normalized
                                : rootTransform.forward;
                        }
                    }

                    lastValidPoint = worldPos;
                } else {
                    // Padding: If path ends, repeat the last valid point
                    worldPos = lastValidPoint;
                    worldForward = rootTransform.forward;
                }

                // 2. Transform to Local Space (Agent-Centric)
                Vector3 localPos = rootTransform.InverseTransformPoint(worldPos);
                Vector3 localForward = rootTransform.InverseTransformDirection(worldForward);

                // 3. Pack into Buffer (5 dimensions per token)
                // Feature 1-3: Relative Position
                navigationBuffer[bufferIndex++] = localPos.x;
                navigationBuffer[bufferIndex++] = localPos.y;
                navigationBuffer[bufferIndex++] = localPos.z;

                // Feature 4-5: Relative Forward (Projected on XZ plane for flow)
                navigationBuffer[bufferIndex++] = localForward.x;
                navigationBuffer[bufferIndex++] = localForward.z;
            }
        }

        private void OnDrawGizmosSelected() {
            if (rootTransform == null || navigationBuffer == null) {
                return;
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < sampleCount; i++) {
                int idx = i * 5;
                Vector3 localPos = new(navigationBuffer[idx], navigationBuffer[idx + 1], navigationBuffer[idx + 2]);
                Vector3 worldPos = rootTransform.TransformPoint(localPos);
                Gizmos.DrawSphere(worldPos, 0.2f);
            }
        }
    }
}
