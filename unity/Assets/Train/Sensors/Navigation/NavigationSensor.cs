using System;
using Train.Navigation.Scripts;
using UnityEngine;

namespace Train.Sensors.Navigation {
    public class NavigationSensor : MonoBehaviour {
        [Header("References")] [SerializeField]
        private Navigator navigator;

        [SerializeField] private Transform agentRoot;

        [Header("Normalization Settings")] [SerializeField]
        private float emc = 20.0f;

        [SerializeField] private int sampleCount = 10;

        private float[] _navigationBuffer;

        public NavigationSensor(float[] navigationBuffer) => _navigationBuffer = navigationBuffer;

        private void Awake() {
            if (agentRoot == null) {
                agentRoot = transform;
            }

            _navigationBuffer = new float[sampleCount * 5];
        }

        public void UpdateNavigationData() {
            if (navigator == null) {
                return;
            }

            // 1. Calculate the 'Stable Heading' (Projected on World Horizontal Plane)
            // Use Vector3.ProjectOnPlane to remove any Y (Vertical) component from the forward vector
            Vector3 projectedForward = Vector3.ProjectOnPlane(agentRoot.forward, Vector3.up).normalized;

            // If the agent is looking directly up/down, fallback to its raw forward
            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = agentRoot.forward;
            }

            // Create a rotation that is always upright relative to World Up
            Quaternion stableRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            Matrix4x4 stableMatrix = Matrix4x4.TRS(agentRoot.position, stableRotation, Vector3.one);
            Matrix4x4 worldToStable = stableMatrix.inverse;

            Vector3[] corners = navigator.Corners;
            int bufferIndex = 0;
            Vector3 lastValidPoint = agentRoot.position;
            Vector3 lastValidDir = projectedForward;

            for (int i = 0; i < sampleCount; i++) {
                Vector3 worldPos;
                Vector3 worldDir;

                if (corners != null && i < corners.Length) {
                    worldPos = corners[i];
                    worldDir = i + 1 < corners.Length
                        ? (corners[i + 1] - corners[i]).normalized
                        : i > 0
                            ? (corners[i] - corners[i - 1]).normalized
                            : projectedForward;

                    lastValidPoint = worldPos;
                    lastValidDir = worldDir;
                } else {
                    worldPos = lastValidPoint;
                    worldDir = lastValidDir;
                }

                // 2. Transform to Stable Local Space
                // Instead of InverseTransformPoint, use our custom stable matrix
                Vector3 localPos = worldToStable.MultiplyPoint3x4(worldPos);
                Vector3 localDir = worldToStable.MultiplyVector(worldDir);

                // 3. User-Defined Token Mapping (Stable Coordinates)
                // User X: Forward/Backward (Stable Z)
                // User Y: Left/Right (Stable X)
                // User Z: Height/Slope (Stable Y)
                float relPosX = localPos.z;
                float relPosY = localPos.x;
                float relPosZ = localPos.y;

                // 4. Normalization with Tanh: $\tanh(\text{Value} / \text{EMC})$
                _navigationBuffer[bufferIndex++] = (float)Math.Tanh(relPosX / emc);
                _navigationBuffer[bufferIndex++] = (float)Math.Tanh(relPosY / emc);
                _navigationBuffer[bufferIndex++] = (float)Math.Tanh(relPosZ / emc);

                // 5. Target Direction (Relative to Stable Heading)
                _navigationBuffer[bufferIndex++] = localDir.z; // Target Dir X (Forward component)
                _navigationBuffer[bufferIndex++] = localDir.x; // Target Dir Y (Side component)
            }
        }
    }
}
