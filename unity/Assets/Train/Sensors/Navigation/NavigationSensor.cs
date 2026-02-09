using System;
using Train.Navigation.Scripts;
using UnityEngine;

namespace Train.Sensors.Navigation {
    public class NavigationSensor : MonoBehaviour {
        [Header("Dependencies")] [SerializeField]
        private Navigator navigator;

        [SerializeField] private Proprioception proprioception;

        [Header("Encoder Specs")] [SerializeField]
        private int maxToken = 3;

        [SerializeField] private int inputDim = 5;

        [Header("Normalization")] [SerializeField]
        private float emc = 20.0f;

        private float[] _navigationBuffer;
        public float[] NavigationBuffer => _navigationBuffer;

        private void Awake() =>
            _navigationBuffer = new float[maxToken * inputDim];

        public void UpdateNavigationData() {
            if (navigator == null || proprioception == null) {
                return;
            }

            Vector3 agentPos = proprioception.Position;
            Vector3 rawForward = proprioception.Forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(rawForward, Vector3.up).normalized;
            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = rawForward;
            }

            Quaternion stableRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            Matrix4x4 worldToStable = Matrix4x4.TRS(agentPos, stableRotation, Vector3.one).inverse;

            Vector3[] corners = navigator.Corners;
            int bufferIdx = 0;

            Vector3 lastValidPoint = agentPos;
            Vector3 lastValidDir = projectedForward;

            for (int i = 0; i < maxToken; i++) {
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

                Vector3 localPos = worldToStable.MultiplyPoint3x4(worldPos);
                Vector3 localDir = worldToStable.MultiplyVector(worldDir);

                _navigationBuffer[bufferIdx++] = (float)Math.Tanh(localPos.z / emc); // Forward distance
                _navigationBuffer[bufferIdx++] = (float)Math.Tanh(localPos.x / emc); // Lateral distance
                _navigationBuffer[bufferIdx++] = (float)Math.Tanh(localPos.y / emc); // Vertical (Slope)

                _navigationBuffer[bufferIdx++] = localDir.z; // Target forward direction component
                _navigationBuffer[bufferIdx++] = localDir.x; // Target side direction component
            }
        }
    }
}
